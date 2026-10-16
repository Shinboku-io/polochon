using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Modules;
using Polochon.Messaging;
using Polochon.Modules;
using Polochon.Tests.Domain;
using Xunit;
using Recorder = Polochon.Tests.Messaging.InboxProcessorTests.Recorder;

namespace Polochon.Tests.Messaging
{
    /// <summary>
    /// Tests for the on-demand inbox drain: <see cref="ModuleBase.ProcessInboxAsync"/>,
    /// <see cref="EventBus.RelayPendingMessagesAsync"/> and the host-level
    /// <see cref="Polochon.Messaging.ServiceRegister.DrainInboxAsync{TModule}(IServiceProvider, CancellationToken)"/>. Reuses the event-to-command handlers
    /// declared in <see cref="InboxProcessorTests"/>, picked up by every module scanning this assembly.
    /// </summary>
    public sealed class InboxDrainTests
    {
        /// <summary>Drains everything in the inbox in one call, and reports the count.</summary>
        [Fact(DisplayName = "ProcessInboxAsync handles every message in the inbox and reports them")]
        public async Task ProcessInboxAsyncHandlesEveryMessage()
        {
            await using var module = new DrainModule();
            await module.InitializeAsync();
            var inbox = module.GetRequiredService<IInboxWriter>();
            await inbox.PublishMessageAsync(new TestIntegrationEvent("first"), CancellationToken.None);
            await inbox.PublishMessageAsync(new TestIntegrationEvent("second"), CancellationToken.None);

            var result = await module.ProcessInboxAsync();

            Assert.Equal(new InboxDrainResult(2, 0), result);
            Assert.Equal(2, result.Succeeded);
            Assert.Equal(["first", "second"], module.GetRequiredService<Recorder>().Received);
            Assert.Equal(new InboxDrainResult(0, 0), await module.ProcessInboxAsync());
        }

        /// <summary>A failing message is counted, routed to the error queue, and does not stop the drain.</summary>
        [Fact(DisplayName = "ProcessInboxAsync counts a failing message, routes it to the error queue and goes on")]
        public async Task ProcessInboxAsyncRoutesFailureToErrorQueue()
        {
            await using var module = new DrainModule();
            await module.InitializeAsync();
            var inbox = module.GetRequiredService<IInboxWriter>();
            await inbox.PublishMessageAsync(new TestIntegrationEvent("boom"), CancellationToken.None);
            await inbox.PublishMessageAsync(new TestIntegrationEvent("after"), CancellationToken.None);

            var result = await module.ProcessInboxAsync();

            Assert.Equal(new InboxDrainResult(2, 1), result);
            Assert.Equal(["after"], module.GetRequiredService<Recorder>().Received);
            var failure = Assert.Single(((MemoryErrorQueue)module.GetRequiredService<IErrorQueue>()).Failures);
            Assert.Equal("boom", Assert.IsType<TestIntegrationEvent>(failure.Message).Payload);
        }

        /// <summary>The module's container only exists once initialized.</summary>
        [Fact(DisplayName = "ProcessInboxAsync on a module not initialized throws")]
        public async Task ProcessInboxAsyncBeforeInitializationThrows()
        {
            await using var module = new DrainModule();

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => module.ProcessInboxAsync());
        }

        /// <summary>
        /// The host-level drain handles the module's inbox, then relays what sits in the outboxes
        /// to the other modules' inboxes, without waiting for the bus clock.
        /// </summary>
        [Fact(DisplayName = "DrainInboxAsync drains the module's inbox then relays pending outbox messages")]
        public async Task DrainInboxAsyncDrainsThenRelays()
        {
            var services = new ServiceCollection();
            _ = services.AddPolochon();
            _ = services.AddModule<DrainModule>();
            _ = services.AddModule<OtherModule>();
            await using var provider = services.BuildServiceProvider();
            var modules = provider.GetServices<IModularModule>().ToArray();
            foreach (var module in modules)
            {
                await module.InitializeAsync();
            }

            var drained = modules.OfType<DrainModule>().Single();
            var other = modules.OfType<OtherModule>().Single();
            await drained.GetRequiredService<IInboxWriter>().PublishMessageAsync(new TestIntegrationEvent("to-drain"), CancellationToken.None);

            // Stands in for an integration event a handler published while the inbox was drained.
            await drained.GetRequiredService<IOutboxWriter>().PublishMessageAsync(new TestIntegrationEvent("published"), CancellationToken.None);

            var result = await provider.DrainInboxAsync<DrainModule>();

            Assert.Equal(new InboxDrainResult(1, 0), result);
            Assert.Equal(["to-drain"], drained.GetRequiredService<Recorder>().Received);
            Assert.Equal(new InboxDrainResult(1, 0), await other.ProcessInboxAsync());
            Assert.Equal(["published"], other.GetRequiredService<Recorder>().Received);
        }

        /// <summary>Guards against a missing provider.</summary>
        [Fact(DisplayName = "DrainInboxAsync without a service provider throws")]
        public async Task DrainInboxAsyncWithoutProviderThrows()
        {
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => ((IServiceProvider)null!).DrainInboxAsync<DrainModule>());
        }

        private sealed class DrainModule : ModuleBase
        {
            public DrainModule()
                : base("drain", [typeof(InboxDrainTests).Assembly])
            {
            }

            protected override void ConfigureAdditionalServices(IServiceCollection services)
            {
                // IntegrationEventToCommandHandlerTests also reacts to TestIntegrationEvent in every
                // module scanning this assembly; its command handler needs its own recorder.
                services.AddSingleton<Recorder>();
                services.AddSingleton<IntegrationEventToCommandHandlerTests.Recorder>();
            }
        }

        private sealed class OtherModule : ModuleBase
        {
            public OtherModule()
                : base("other", [typeof(InboxDrainTests).Assembly])
            {
            }

            protected override void ConfigureAdditionalServices(IServiceCollection services)
            {
                // IntegrationEventToCommandHandlerTests also reacts to TestIntegrationEvent in every
                // module scanning this assembly; its command handler needs its own recorder.
                services.AddSingleton<Recorder>();
                services.AddSingleton<IntegrationEventToCommandHandlerTests.Recorder>();
            }
        }
    }
}
