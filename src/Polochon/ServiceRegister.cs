using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Logging;
using Polochon.Mediation;
using Polochon.Messaging;
using Polochon.Modules;
using Polochon.Security;

namespace Polochon
{
    /// <summary>
    /// Registers Polochon services into the dependency injection container.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Adds Polochon services to the specified IServiceCollection. Designed to be used during application startup on the host.
        /// </summary>
        /// <param name="services">The IServiceCollection to which Polochon services will be added.</param>
        /// <returns>The updated IServiceCollection with Polochon services registered.</returns>
        public static IServiceCollection AddPolochon(this IServiceCollection services)
        {
            return services
                .AddPolochonLogging()
                .AddPolochonUserIdentity()
                .AddSingleton<IPolochonDispatcher, PolochonRouter>()
                .AddSingleton<EventBus>()
                .AddHostedService(provider => provider.GetRequiredService<EventBus>())
                .AddHostedService<LifecycleService>();
        }
    }
}