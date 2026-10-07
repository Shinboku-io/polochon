using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Modules;
using Polochon.Modules;
using Xunit;

namespace Polochon.Messaging.AzureQueue.Tests
{
    /// <summary>
    /// Tests for <see cref="ServiceRegister.WithAzureQueueInbox{TModule}"/>: the module-builder
    /// extension that swaps a module's default in-memory <see cref="IInbox"/> and
    /// <see cref="IErrorQueue"/> for an Azure Storage Queue-backed pair.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        private sealed class FakeModule : ModuleBase
        {
            public FakeModule()
                : base("fake-azure-queue", [typeof(FakeModule).Assembly])
            {
            }
        }

        private static PersistentInboxOptions CreateOptions() => new()
        {
            QueueEndpoint = "https://fake.queue.core.windows.net",
            InboxQueueName = "inbox",
        };

        /// <summary><c>WithAzureQueueInbox</c> makes the module resolve an <see cref="AzureQueueInbox"/> as its <see cref="IInbox"/>.</summary>
        [Fact]
        public async Task WithAzureQueueInbox_MakesModuleResolveAzureQueueInbox_AsIInbox()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = services.AddModule<FakeModule>();
            _ = builder.WithAzureQueueInbox(CreateOptions());

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            Assert.IsType<AzureQueueInbox>(module.GetRequiredService<IInbox>());
        }

        /// <summary>
        /// <c>WithAzureQueueInbox</c> resolves <see cref="IInbox"/> and <see cref="IErrorQueue"/>
        /// to the very same <see cref="AzureQueueInbox"/> instance - a processing failure and a
        /// message that could not be read both end up in the same Azure error queue.
        /// </summary>
        [Fact]
        public async Task WithAzureQueueInbox_ResolvesIInboxAndIErrorQueue_ToTheSameInstance()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = services.AddModule<FakeModule>();
            _ = builder.WithAzureQueueInbox(CreateOptions());

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            Assert.Same(module.GetRequiredService<IInbox>(), module.GetRequiredService<IErrorQueue>());
        }

        /// <summary><c>WithAzureQueueInbox</c> returns the same builder instance for chaining.</summary>
        [Fact]
        public void WithAzureQueueInbox_ReturnsSameBuilderInstance()
        {
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();

            var result = builder.WithAzureQueueInbox(CreateOptions());

            Assert.Same(builder, result);
        }

        /// <summary><c>WithAzureQueueInbox</c> throws when given a null options instance.</summary>
        [Fact]
        public void WithAzureQueueInbox_WithNullOptions_Throws()
        {
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();

            Assert.Throws<ArgumentNullException>(() => builder.WithAzureQueueInbox(null!));
        }
    }
}
