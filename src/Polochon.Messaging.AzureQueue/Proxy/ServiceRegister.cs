using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Registers the plumbing shared by every Azure Storage Queue-backed component in this
    /// package (<see cref="IQueueClientProxyFactory"/> and <see cref="ICredentialFactory"/>).
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers <see cref="IQueueClientProxyFactory"/> and <see cref="ICredentialFactory"/> if
        /// nothing else already has - safe to call more than once on the same module (e.g. if a
        /// module's own registration also needs the proxy directly), since only the first
        /// registration of each is kept.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        public static IServiceCollection AddAzureQueueProxy(this IServiceCollection services)
        {
            services.TryAddSingleton<ICredentialFactory, DefaultAzureCredentialFactory>();
            services.TryAddTransient<IQueueClientProxyFactory, QueueClientProxyFactory>();

            return services;
        }
    }
}