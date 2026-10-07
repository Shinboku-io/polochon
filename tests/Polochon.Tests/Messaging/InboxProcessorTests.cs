using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Results;
using Polochon.Messaging;
using Polochon.Modules;
using Polochon.Tests.Domain;
using Xunit;

namespace Polochon.Tests.Messaging
{
    /// <summary>
    /// Tests for <see cref="InboxProcessor"/> and the <c>WithInboxProcessing</c> registration
    /// extension: a message published into a module's own inbox is picked up on the next clock
    /// tick and dispatched through that module's notification pipeline - end to end, the same path
    /// an <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/> is built for.
    /// </summary>
    public sealed class InboxProcessorTests
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

        /// <summary>Handles the mapped command by recording it - stands in for real business logic.</summary>
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
                if (command.Payload == "boom")
                {
                    throw new InvalidOperationException("Simulated handler failure.");
                }

                recorder.Received.Add(command.Payload);
                return ValueTask.FromResult(CommandResult.Success());
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

        /// <summary>Minimal module used to exercise a real, initialized module's inbox and its processor.</summary>
        private sealed class FakeModule : ModuleBase
        {
            public FakeModule()
                : base("fake-inbox", [typeof(InboxProcessorTests).Assembly])
            {
            }

            protected override void ConfigureAdditionalServices(IServiceCollection services)
            {
                services.AddSingleton<Recorder>();
            }
        }

        /// <summary>The default clock is documented as one second.</summary>
        [Fact]
        public void DefaultClock_IsOneSecond()
        {
            Assert.Equal(TimeSpan.FromSeconds(1), InboxProcessor.DefaultClock);
        }

        /// <summary>
        /// A message published into the module's own inbox - as the cross-module EventBus would -
        /// is drained on the next clock tick and dispatched through the module's notification
        /// pipeline, ending up at the mapped command's own handler.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_DrainsInboxOnClockTick_AndDispatchesMappedCommand()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();

            var inboxWriter = module.GetRequiredService<IInboxWriter>();
            await inboxWriter.PublishMessageAsync(new TestIntegrationEvent("via-inbox"), CancellationToken.None);

            var processor = new InboxProcessor(module, TimeSpan.FromMilliseconds(20), NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                var recorder = module.GetRequiredService<Recorder>();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (recorder.Received.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(10);
                }

                Assert.Equal(["via-inbox"], recorder.Received);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// When the mapped command's handler throws, the module reports it as a failed result, and
        /// the original event is routed to the module's <see cref="IErrorQueue"/> instead of being
        /// silently dropped.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_WhenHandlerThrows_RoutesFailureToErrorQueue()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();

            var inboxWriter = module.GetRequiredService<IInboxWriter>();
            await inboxWriter.PublishMessageAsync(new TestIntegrationEvent("boom"), CancellationToken.None);

            var processor = new InboxProcessor(module, TimeSpan.FromMilliseconds(20), NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                var errorQueue = (MemoryErrorQueue)module.GetRequiredService<IErrorQueue>();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (errorQueue.Failures.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(10);
                }

                var failure = Assert.Single(errorQueue.Failures);
                var failedEvent = Assert.IsType<TestIntegrationEvent>(failure.Message);
                Assert.Equal("boom", failedEvent.Payload);
                var exception = Assert.IsType<IntegrationEventHandlingException>(failure.Exception);
                Assert.Equal(ResultCode.UnexpectedError, exception.Result.Result);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary><c>WithInboxProcessing</c> registers an <see cref="InboxProcessor"/> as a host-level hosted service.</summary>
        [Fact]
        public void WithInboxProcessing_RegistersInboxProcessor_AsAHostedService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = services.AddModule<FakeModule>();
            _ = builder.WithInboxProcessing(TimeSpan.FromMilliseconds(5));

            using var provider = services.BuildServiceProvider();

            Assert.Contains(provider.GetServices<IHostedService>(), hostedService => hostedService is InboxProcessor);
        }

        /// <summary><c>WithInboxProcessing</c> returns the same builder instance for chaining.</summary>
        [Fact]
        public void WithInboxProcessing_ReturnsSameBuilderInstance()
        {
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();

            var result = builder.WithInboxProcessing();

            Assert.Same(builder, result);
        }
    }
}
