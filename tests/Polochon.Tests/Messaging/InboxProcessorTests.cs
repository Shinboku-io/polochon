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

        /// <summary>In-process mode reads the inbox once at startup, then only when a message is delivered.</summary>
        [Fact(DisplayName = "In-process mode reads the inbox once at startup, then only when a message is delivered")]
        public async Task InProcessModeReadsInboxOnlyOnStartupAndDelivery()
        {
            await using var module = new CountingModule();
            await module.InitializeAsync();
            var inbox = (CountingInbox)module.GetRequiredService<IInbox>();
            var recorder = module.GetRequiredService<Recorder>();

            var processor = new InboxProcessor(module, new InboxProcessingOptions { Mode = InboxProcessingMode.InProcess }, NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                await WaitUntilAsync(() => inbox.ReadCount >= 1);

                // Many polling clocks' worth of idle time: an in-process processor must not read.
                await Task.Delay(300);
                Assert.Equal(1, inbox.ReadCount);

                await module.Inbox.PublishMessageAsync(new TestIntegrationEvent("delivered"), CancellationToken.None);
                await WaitUntilAsync(() => recorder.Received.Count == 1);

                Assert.Equal(["delivered"], recorder.Received);
                Assert.Equal(2, inbox.ReadCount);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>In-process mode processes messages already in the inbox when it starts.</summary>
        [Fact(DisplayName = "In-process mode processes messages already in the inbox when it starts")]
        public async Task InProcessModeProcessesMessagesLeftInInboxAtStartup()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();

            // Written straight to the inbox, as a previous run would have left it: no signal raised.
            await module.GetRequiredService<IInboxWriter>().PublishMessageAsync(new TestIntegrationEvent("left-over"), CancellationToken.None);

            var processor = new InboxProcessor(module, new InboxProcessingOptions { Mode = InboxProcessingMode.InProcess }, NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                var recorder = module.GetRequiredService<Recorder>();
                await WaitUntilAsync(() => recorder.Received.Count == 1);

                Assert.Equal(["left-over"], recorder.Received);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>In-process mode without idle sweep leaves an unsignaled message until the next delivery.</summary>
        [Fact(DisplayName = "In-process mode without idle sweep leaves an unsignaled message until the next delivery")]
        public async Task InProcessModeWithoutIdleSweepWaitsForNextDelivery()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<Recorder>();

            var processor = new InboxProcessor(module, new InboxProcessingOptions { Mode = InboxProcessingMode.InProcess }, NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                // Let the startup read pass, then write without signaling.
                await Task.Delay(100);
                await module.GetRequiredService<IInboxWriter>().PublishMessageAsync(new TestIntegrationEvent("unsignaled"), CancellationToken.None);
                await Task.Delay(300);
                Assert.Empty(recorder.Received);

                await module.Inbox.PublishMessageAsync(new TestIntegrationEvent("signaled"), CancellationToken.None);
                await WaitUntilAsync(() => recorder.Received.Count == 2);

                Assert.Equal(["unsignaled", "signaled"], recorder.Received);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>In-process mode with idle sweep reads an unsignaled message once idle.</summary>
        [Fact(DisplayName = "In-process mode with idle sweep reads an unsignaled message once idle")]
        public async Task InProcessModeWithIdleSweepReadsUnsignaledMessage()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<Recorder>();

            var options = new InboxProcessingOptions { Mode = InboxProcessingMode.InProcess, IdleSweep = TimeSpan.FromMilliseconds(50) };
            var processor = new InboxProcessor(module, options, NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                await Task.Delay(100);
                await module.GetRequiredService<IInboxWriter>().PublishMessageAsync(new TestIntegrationEvent("swept"), CancellationToken.None);
                await WaitUntilAsync(() => recorder.Received.Count == 1);

                Assert.Equal(["swept"], recorder.Received);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>Polling mode set through options reads the inbox on its clock.</summary>
        [Fact(DisplayName = "Polling mode set through options reads the inbox on its clock")]
        public async Task PollingModeThroughOptionsReadsOnClock()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<Recorder>();

            var options = new InboxProcessingOptions { Mode = InboxProcessingMode.Polling, Clock = TimeSpan.FromMilliseconds(20) };
            var processor = new InboxProcessor(module, options, NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                await module.GetRequiredService<IInboxWriter>().PublishMessageAsync(new TestIntegrationEvent("polled"), CancellationToken.None);
                await WaitUntilAsync(() => recorder.Received.Count == 1);

                Assert.Equal(["polled"], recorder.Received);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>Inbox processing options default to polling on the default clock without idle sweep.</summary>
        [Fact(DisplayName = "Inbox processing options default to polling on the default clock without idle sweep")]
        public void InboxProcessingOptionsDefaultToPolling()
        {
            var options = new InboxProcessingOptions();

            Assert.Equal(InboxProcessingMode.Polling, options.Mode);
            Assert.Equal(InboxProcessor.DefaultClock, options.Clock);
            Assert.Null(options.IdleSweep);
        }

        /// <summary>Invalid inbox processing options are rejected.</summary>
        [Fact(DisplayName = "Invalid inbox processing options are rejected")]
        public async Task InvalidInboxProcessingOptionsAreRejected()
        {
            await using var module = new FakeModule();

            InboxProcessingOptions[] invalid =
            [
                new() { Mode = InboxProcessingMode.Polling, Clock = TimeSpan.Zero },
                new() { Mode = InboxProcessingMode.InProcess, IdleSweep = TimeSpan.FromSeconds(-1) },
                new() { Mode = (InboxProcessingMode)42 },
            ];

            foreach (var options in invalid)
            {
                _ = Assert.Throws<ArgumentOutOfRangeException>(() => new InboxProcessor(module, options, NullLogger<InboxProcessor>.Instance));
                _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddModule<FakeModule>().WithInboxProcessing(options));
            }

            _ = Assert.Throws<ArgumentNullException>(() => new InboxProcessor(module, (InboxProcessingOptions)null!, NullLogger<InboxProcessor>.Instance));
            _ = Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddModule<FakeModule>().WithInboxProcessing((InboxProcessingOptions)null!));
        }

        /// <summary>Polling mode ignores the idle sweep and in-process mode ignores the clock.</summary>
        [Fact(DisplayName = "Polling mode ignores the idle sweep and in-process mode ignores the clock")]
        public async Task EachModeIgnoresTheOtherModeSetting()
        {
            await using var module = new FakeModule();

            _ = new InboxProcessor(module, new InboxProcessingOptions { Mode = InboxProcessingMode.Polling, IdleSweep = TimeSpan.Zero }, NullLogger<InboxProcessor>.Instance);
            _ = new InboxProcessor(module, new InboxProcessingOptions { Mode = InboxProcessingMode.InProcess, Clock = TimeSpan.Zero }, NullLogger<InboxProcessor>.Instance);
        }

        /// <summary>WithInboxProcessing with options registers an inbox processor and returns the same builder.</summary>
        [Fact(DisplayName = "WithInboxProcessing with options registers an inbox processor and returns the same builder")]
        public void WithInboxProcessingOptionsRegistersInboxProcessor()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = services.AddModule<FakeModule>();

            var result = builder.WithInboxProcessing(new InboxProcessingOptions { Mode = InboxProcessingMode.InProcess });

            Assert.Same(builder, result);
            using var provider = services.BuildServiceProvider();
            Assert.Contains(provider.GetServices<IHostedService>(), hostedService => hostedService is InboxProcessor);
        }

        /// <summary>The inbox signal coalesces deliveries raised before a wait.</summary>
        [Fact(DisplayName = "The inbox signal coalesces deliveries raised before a wait")]
        public async Task InboxSignalCoalescesDeliveries()
        {
            var signal = new InboxSignal();
            signal.Notify();
            signal.Notify();

            await signal.WaitAsync(CancellationToken.None);

            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await signal.WaitAsync(timeout.Token));
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }

        /// <summary>In-memory inbox counting how often it is read, to tell polling from signaled reads.</summary>
        private sealed class CountingInbox : IInbox
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<Abstractions.Domain.IIntegrationEvent> messages = new();
            private int readCount;

            public int ReadCount => Volatile.Read(ref readCount);

            public async IAsyncEnumerable<Abstractions.Domain.IIntegrationEvent> ReadAllMessagesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
            {
                _ = Interlocked.Increment(ref readCount);
                while (messages.TryDequeue(out var message))
                {
                    yield return message;
                }

                await Task.CompletedTask;
            }

            public ValueTask PublishMessageAsync(Abstractions.Domain.IIntegrationEvent message, CancellationToken cancellationToken)
            {
                messages.Enqueue(message);
                return ValueTask.CompletedTask;
            }
        }

        /// <summary><see cref="FakeModule"/> with a <see cref="CountingInbox"/> in place of its default inbox.</summary>
        private sealed class CountingModule : ModuleBase
        {
            public CountingModule()
                : base("counting-inbox", [typeof(InboxProcessorTests).Assembly])
            {
            }

            protected override void ConfigureAdditionalServices(IServiceCollection services)
            {
                services.AddSingleton<Recorder>();
                services.AddSingleton<IInbox, CountingInbox>();
            }
        }
    }
}
