using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Results;
using Polochon.Mediation;
using Polochon.Tests.Domain;
using Xunit;

namespace Polochon.Tests.Messaging
{
    /// <summary>
    /// Tests for <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/>: a derived class
    /// is discovered and wired up by the same <c>AddDispatcher</c> scan as any other
    /// <see cref="INotificationHandler{TNotification}"/> - no dedicated registration is needed.
    /// </summary>
    public sealed class IntegrationEventToCommandHandlerTests
    {
        /// <summary>The command <see cref="TestEventToCommandHandler"/> maps <see cref="TestIntegrationEvent"/> to.</summary>
        public sealed record TestCommand : ICommand
        {
            /// <summary>Creates the command with the given payload.</summary>
            public TestCommand(string payload)
            {
                Payload = payload;
            }

            /// <summary>An arbitrary marker value, carried over from the source event.</summary>
            public string Payload { get; init; }
        }

        /// <summary>Shared, DI-resolved sink that the command handler appends to, so tests can assert on it.</summary>
        public sealed class Recorder
        {
            /// <summary>The payloads received so far, in handling order.</summary>
            public List<string> Received { get; } = [];
        }

        /// <summary>The failure <see cref="TestCommandHandler"/> reports for the <c>"fail"</c> payload.</summary>
        public static readonly ResultCode RejectedCode = new() { Code = -100, Status = "REJECTED" };

        /// <summary>
        /// Handles the mapped command by recording it - stands in for real business logic. Implements
        /// the <see cref="ICommandHandler{TCommand}"/> alias only, so it must be found by the scan.
        /// </summary>
        public sealed class TestCommandHandler : ICommandHandler<TestCommand>
        {
            private readonly Recorder recorder;

            /// <summary>Creates the handler with the shared recorder to append to.</summary>
            public TestCommandHandler(Recorder recorder)
            {
                this.recorder = recorder;
            }

            /// <inheritdoc/>
            public ValueTask<CommandResult> HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
            {
                recorder.Received.Add(command.Payload);
                return ValueTask.FromResult(command.Payload == "fail" ? CommandResult.Failure(RejectedCode) : CommandResult.Success());
            }
        }

        /// <summary>Records every result it is handed instead of throwing on a failure.</summary>
        public sealed class ResultRecorder
        {
            /// <summary>The results received so far, in handling order.</summary>
            public List<CommandResult> Results { get; } = [];
        }

        /// <summary>An event of its own, so its event handler is the only one reacting to <see cref="ObservedEvent"/>.</summary>
        public sealed record ObservedEvent : IIntegrationEvent
        {
            /// <summary>Creates the event with the given payload.</summary>
            public ObservedEvent(string payload)
            {
                Payload = payload;
            }

            /// <summary>An arbitrary marker value.</summary>
            public string Payload { get; init; }

            /// <inheritdoc/>
            public Guid Id { get; init; } = Guid.NewGuid();

            /// <inheritdoc/>
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        }

        /// <summary>Overrides the result hook to record the outcome instead of throwing.</summary>
        public sealed class ObservingEventToCommandHandler : IntegrationEventToCommandHandler<ObservedEvent, TestCommand>
        {
            private readonly ResultRecorder results;

            /// <summary>Creates the handler.</summary>
            public ObservingEventToCommandHandler(IPolochonDispatcher dispatcher, ResultRecorder results)
                : base(dispatcher)
            {
                this.results = results;
            }

            /// <inheritdoc/>
            protected override TestCommand Map(ObservedEvent integrationEvent) => new(integrationEvent.Payload);

            /// <inheritdoc/>
            protected override ValueTask OnCommandHandledAsync(ObservedEvent integrationEvent, TestCommand command, CommandResult result, CancellationToken cancellationToken)
            {
                results.Results.Add(result);
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>Translates <see cref="TestIntegrationEvent"/> into <see cref="TestCommand"/>.</summary>
        public sealed class TestEventToCommandHandler : IntegrationEventToCommandHandler<TestIntegrationEvent, TestCommand>
        {
            /// <summary>Creates the handler, forwarding the dispatcher to the base class.</summary>
            public TestEventToCommandHandler(IPolochonDispatcher dispatcher)
                : base(dispatcher)
            {
            }

            /// <inheritdoc/>
            protected override TestCommand Map(TestIntegrationEvent integrationEvent)
                => new(integrationEvent.Payload);
        }

        /// <summary>
        /// Publishing the integration event ends up invoking the mapped command's own handler -
        /// proving the event was translated and dispatched, not handled inline.
        /// </summary>
        [Fact]
        public async Task PublishAsync_WithIntegrationEventToCommandHandlerRegistered_DispatchesMappedCommand()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher(typeof(TestEventToCommandHandler), typeof(TestCommandHandler));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            await publisher.PublishAsync(new TestIntegrationEvent("from-another-module"));

            Assert.Equal(["from-another-module"], provider.GetRequiredService<Recorder>().Received);
        }

        /// <summary>
        /// A module registering an <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/>
        /// is reported as able to handle that integration event, the same as it would be for a
        /// plain <see cref="INotificationHandler{TNotification}"/> - used by inbox routing.
        /// </summary>
        [Fact]
        public void DispatcherRegistry_WithIntegrationEventToCommandHandlerRegistered_IncludesEventType()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher(typeof(TestEventToCommandHandler), typeof(TestCommandHandler));

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<DispatcherRegistry>();

            Assert.True(registry.NotificationWrappers.ContainsKey(typeof(TestIntegrationEvent)));
        }

        /// <summary>A handler implementing only the <see cref="ICommandHandler{TCommand}"/> alias is registered by the scan.</summary>
        [Fact(DisplayName = "Scan registers a handler implementing only the ICommandHandler<TCommand> alias")]
        public async Task ScanRegistersAliasOnlyCommandHandler()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher(typeof(TestCommandHandler));

            var provider = services.BuildServiceProvider();
            var result = await provider.GetRequiredService<IPolochonDispatcher>().SendCommandAsync(new TestCommand("direct"), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["direct"], provider.GetRequiredService<Recorder>().Received);
        }

        /// <summary>By default, a failed result of the mapped command throws so the event is reported as failed.</summary>
        [Fact(DisplayName = "Failed mapped command throws IntegrationEventHandlingException by default")]
        public async Task FailedMappedCommandThrowsByDefault()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher(typeof(TestEventToCommandHandler), typeof(TestCommandHandler));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var exception = await Assert.ThrowsAsync<IntegrationEventHandlingException>(
                async () => await publisher.PublishAsync(new TestIntegrationEvent("fail"), TestContext.Current.CancellationToken));

            Assert.Equal(RejectedCode, exception.Result.Result);
        }

        /// <summary>An overridden result hook receives every result, failures included, without throwing.</summary>
        [Fact(DisplayName = "Overridden OnCommandHandledAsync receives the result of the mapped command")]
        public async Task OverriddenHookReceivesResult()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddSingleton<ResultRecorder>();
            services.AddDispatcher(typeof(ObservingEventToCommandHandler), typeof(TestCommandHandler));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            await publisher.PublishAsync(new ObservedEvent("ok"), TestContext.Current.CancellationToken);
            await publisher.PublishAsync(new ObservedEvent("fail"), TestContext.Current.CancellationToken);

            var results = provider.GetRequiredService<ResultRecorder>().Results;
            Assert.Equal(2, results.Count);
            Assert.True(results[0].IsSuccess);
            Assert.Equal(RejectedCode, results[1].Result);
        }
    }
}
