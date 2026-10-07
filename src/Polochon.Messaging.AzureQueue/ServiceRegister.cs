using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Modules;
using Polochon.Messaging.AzureQueue.Proxy;

namespace Polochon.Messaging.AzureQueue
{
    /// <summary>
    /// Lets a module swap its default in-memory <see cref="IInbox"/> and <see cref="IErrorQueue"/>
    /// (see <c>MemoryInbox</c>/<c>MemoryErrorQueue</c>) for ones backed by an Azure Storage Queue.
    /// Module-level only - like <c>Polochon.Persistence.SqlServer</c>'s <c>WithSqlServer()</c>, and
    /// unlike <c>Polochon.Serilog</c>'s host-level <c>WithSerilog()</c> - because <see cref="IInbox"/>
    /// and <see cref="IErrorQueue"/> are always registered per module (see <c>AddDispatcher</c>),
    /// never at the host level.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Swaps this module's default in-memory <see cref="IInbox"/> and <see cref="IErrorQueue"/>
        /// for a single <see cref="AzureQueueInbox"/> backing both - so a message that fails
        /// processing (routed through <see cref="IErrorQueue"/>) and a message this inbox could
        /// not even read both end up in the same Azure error queue. Queues a callback (via
        /// <see cref="IModularModuleBuilder{TModule}.ConfigureModule"/>) that runs after the
        /// module's own default registration, so it always wins.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="options">Connects this module's inbox and error queue to a specific Azure Storage Queue account.</param>
        public static IModularModuleBuilder<TModule> WithAzureQueueInbox<TModule>(
            this IModularModuleBuilder<TModule> builder,
            PersistentInboxOptions options)
            where TModule : IModularModule
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            return builder.ConfigureModule((services, _, _) =>
            {
                services.AddAzureQueueProxy();

                services.RemoveAll<IInbox>();
                services.RemoveAll<IErrorQueue>();

                services.AddSingleton(sp => new AzureQueueInbox(
                    sp.GetRequiredService<IQueueClientProxyFactory>(),
                    options,
                    sp.GetRequiredService<ILogger<AzureQueueInbox>>()));
                services.AddSingleton<IInbox>(sp => sp.GetRequiredService<AzureQueueInbox>());
                services.AddSingleton<IErrorQueue>(sp => sp.GetRequiredService<AzureQueueInbox>());
            });
        }
    }
}
