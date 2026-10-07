using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Modules;
using Polochon.Abstractions.Results;
using Polochon.Abstractions.Telemetry;
using Xunit;

namespace Polochon.Tests.Telemetry
{
    /// <summary>
    /// Tests the telemetry every module gets by default: its instruments, and the tracing and timing of
    /// every command and query it handles.
    /// </summary>
    public sealed class DispatchTelemetryTests : IAsyncLifetime
    {
        private const string ModuleName = "telemetry-dispatch-tests";

        private static readonly ResultCode Rejected = new() { Code = -42, Status = "PROBE_REJECTED" };

        private readonly TelemetryModule module = new(ModuleName);
        private readonly ActivityCollector activities = new(PolochonTelemetry.GetModuleSourceName(ModuleName));

        /// <summary>Initializes the module before each test.</summary>
        public async ValueTask InitializeAsync() => await module.InitializeAsync();

        /// <summary>Stops recording and disposes the module after each test.</summary>
        public async ValueTask DisposeAsync()
        {
            activities.Dispose();
            await module.DisposeAsync();
        }

        /// <summary>
        /// Tests that a module's instruments are named after it, so a host collects them by name.
        /// </summary>
        [Fact(DisplayName = "A module registers telemetry instruments named after it")]
        public void ModuleRegistersTelemetryNamedAfterIt()
        {
            // Act
            var telemetry = module.GetRequiredService<IModuleTelemetry>();

            // Assert
            Assert.Equal(ModuleName, telemetry.ModuleName);
            Assert.Equal("Polochon.Modules.telemetry-dispatch-tests", telemetry.ActivitySource.Name);
            Assert.Equal("Polochon.Modules.telemetry-dispatch-tests", telemetry.Meter.Name);
        }

        /// <summary>
        /// Tests that a query is traced with the module, message type and kind.
        /// </summary>
        [Fact(DisplayName = "A query is traced with module, message type and kind")]
        public async Task QueryIsTracedWithModuleMessageTypeAndKind()
        {
            // Act
            _ = await ((IModularModule)module).SendQueryAsync(new TelemetryProbeQuery(), TestContext.Current.CancellationToken);

            // Assert
            var activity = Assert.Single(activities.Activities);
            Assert.Equal(nameof(TelemetryProbeQuery), activity.DisplayName);
            Assert.Equal(ActivityKind.Internal, activity.Kind);
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
            Assert.Equal(ModuleName, activity.GetTagItem(PolochonTelemetry.ModuleTag));
            Assert.Equal(nameof(TelemetryProbeQuery), activity.GetTagItem(PolochonTelemetry.MessageTypeTag));
            Assert.Equal("query", activity.GetTagItem(PolochonTelemetry.MessageKindTag));
        }

        /// <summary>
        /// Tests that every message handled is timed in the message duration histogram, whose count is
        /// the message count - tagged so it can be split per module, message and outcome.
        /// </summary>
        [Fact(DisplayName = "Handled messages are timed in the message duration histogram")]
        public async Task HandledMessagesAreTimedInMessageDurationHistogram()
        {
            // Arrange
            using var collector = new MetricCollector<double>(module.GetRequiredService<IModuleTelemetry>().Meter, PolochonTelemetry.MessageDurationMetric);
            IModularModule dispatcher = module;

            // Act
            _ = await dispatcher.SendQueryAsync(new TelemetryProbeQuery(), TestContext.Current.CancellationToken);
            _ = await dispatcher.SendCommandAsync(new TelemetryProbeCommand(), TestContext.Current.CancellationToken);

            // Assert
            var measurements = collector.GetMeasurementSnapshot();
            Assert.Equal(2, measurements.Count);
            Assert.All(measurements, measurement => Assert.True(measurement.Value >= 0));
            Assert.All(measurements, measurement => Assert.Equal(ModuleName, measurement.Tags[PolochonTelemetry.ModuleTag]));
            Assert.All(measurements, measurement => Assert.False(measurement.Tags.ContainsKey(PolochonTelemetry.ErrorTypeTag)));
            Assert.Equal("query", measurements[0].Tags[PolochonTelemetry.MessageKindTag]);
            Assert.Equal("command", measurements[1].Tags[PolochonTelemetry.MessageKindTag]);
            Assert.Equal(ResultCode.Ok.Status, measurements[1].Tags[PolochonTelemetry.ResultCodeTag]);
        }

        /// <summary>
        /// Tests that a handler exception is recorded on the activity and the metric, then rethrown as is.
        /// </summary>
        [Fact(DisplayName = "A throwing query is recorded as an error and rethrown")]
        public async Task ThrowingQueryIsRecordedAsErrorAndRethrown()
        {
            // Arrange
            using var collector = new MetricCollector<double>(module.GetRequiredService<IModuleTelemetry>().Meter, PolochonTelemetry.MessageDurationMetric);

            // Act
            _ = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await ((IModularModule)module).SendQueryAsync(new FailingTelemetryQuery(), TestContext.Current.CancellationToken));

            // Assert
            var activity = Assert.Single(activities.Activities);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("System.InvalidOperationException", activity.GetTagItem(PolochonTelemetry.ErrorTypeTag));
            Assert.Contains(activity.Events, activityEvent => activityEvent.Name == "exception");
            var measurement = Assert.Single(collector.GetMeasurementSnapshot());
            Assert.Equal("System.InvalidOperationException", measurement.Tags[PolochonTelemetry.ErrorTypeTag]);
        }

        /// <summary>
        /// Tests that a command whose failure CommandResultBehavior turns into a CommandResult is still
        /// traced as an error with its result code: the telemetry behavior runs outermost.
        /// </summary>
        [Fact(DisplayName = "A rejected command is traced as an error with its result code")]
        public async Task RejectedCommandIsTracedAsErrorWithItsResultCode()
        {
            // Act
            var result = await ((IModularModule)module).SendCommandAsync(new RejectedTelemetryCommand(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(Rejected, result.Result);
            var activity = Assert.Single(activities.Activities);
            Assert.Equal("command", activity.GetTagItem(PolochonTelemetry.MessageKindTag));
            Assert.Equal(Rejected.Status, activity.GetTagItem(PolochonTelemetry.ResultCodeTag));
            Assert.Equal(Rejected.Status, activity.GetTagItem(PolochonTelemetry.ErrorTypeTag));
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }

        /// <summary>
        /// Tests that a message a handler sends through its injected dispatcher is traced as a child of
        /// the message being handled, so a request reads as one trace.
        /// </summary>
        [Fact(DisplayName = "A message sent from a handler is traced as a child activity")]
        public async Task MessageSentFromHandlerIsTracedAsChildActivity()
        {
            // Act
            _ = await ((IModularModule)module).SendQueryAsync(new NestingTelemetryQuery(), TestContext.Current.CancellationToken);

            // Assert
            var inner = Assert.Single(activities.Activities, activity => activity.DisplayName == nameof(TelemetryProbeQuery));
            var outer = Assert.Single(activities.Activities, activity => activity.DisplayName == nameof(NestingTelemetryQuery));
            Assert.Equal(outer.SpanId, inner.ParentSpanId);
            Assert.Equal(outer.TraceId, inner.TraceId);
        }

        /// <summary>A query handled successfully.</summary>
        public sealed record TelemetryProbeQuery : IQuery<Unit>;

        /// <summary>A command handled successfully.</summary>
        public sealed record TelemetryProbeCommand : ICommand<CommandResult>;

        /// <summary>A query whose handler throws.</summary>
        public sealed record FailingTelemetryQuery : IQuery<Unit>;

        /// <summary>A command whose handler breaks a business rule.</summary>
        public sealed record RejectedTelemetryCommand : ICommand<CommandResult>;

        /// <summary>A query whose handler sends another query through its injected dispatcher.</summary>
        public sealed record NestingTelemetryQuery : IQuery<Unit>;

        /// <summary>Handles <see cref="TelemetryProbeQuery"/>.</summary>
        public sealed class TelemetryProbeQueryHandler : IQueryHandler<TelemetryProbeQuery, Unit>
        {
            /// <inheritdoc/>
            public ValueTask<Unit> HandleAsync(TelemetryProbeQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(Unit.Value);
        }

        /// <summary>Handles <see cref="TelemetryProbeCommand"/>.</summary>
        public sealed class TelemetryProbeCommandHandler : ICommandHandler<TelemetryProbeCommand, CommandResult>
        {
            /// <inheritdoc/>
            public ValueTask<CommandResult> HandleAsync(TelemetryProbeCommand request, CancellationToken cancellationToken) => ValueTask.FromResult(CommandResult.Success());
        }

        /// <summary>Handles <see cref="FailingTelemetryQuery"/> by throwing.</summary>
        public sealed class FailingTelemetryQueryHandler : IQueryHandler<FailingTelemetryQuery, Unit>
        {
            /// <inheritdoc/>
            public ValueTask<Unit> HandleAsync(FailingTelemetryQuery request, CancellationToken cancellationToken) => throw new InvalidOperationException("Probe failure.");
        }

        /// <summary>Handles <see cref="RejectedTelemetryCommand"/> by breaking a business rule.</summary>
        public sealed class RejectedTelemetryCommandHandler : ICommandHandler<RejectedTelemetryCommand, CommandResult>
        {
            /// <inheritdoc/>
            public ValueTask<CommandResult> HandleAsync(RejectedTelemetryCommand request, CancellationToken cancellationToken) => throw new BusinessRuleException(Rejected);
        }

        /// <summary>Handles <see cref="NestingTelemetryQuery"/> by sending a <see cref="TelemetryProbeQuery"/>.</summary>
        public sealed class NestingTelemetryQueryHandler : IQueryHandler<NestingTelemetryQuery, Unit>
        {
            private readonly IPolochonDispatcher dispatcher;

            /// <summary>
            /// Initializes a new instance of the <see cref="NestingTelemetryQueryHandler"/> class.
            /// </summary>
            /// <param name="dispatcher">The module's dispatcher.</param>
            public NestingTelemetryQueryHandler(IPolochonDispatcher dispatcher)
            {
                this.dispatcher = dispatcher;
            }

            /// <inheritdoc/>
            public ValueTask<Unit> HandleAsync(NestingTelemetryQuery request, CancellationToken cancellationToken)
                => dispatcher.SendQueryAsync(new TelemetryProbeQuery(), cancellationToken);
        }
    }
}
