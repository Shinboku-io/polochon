using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Logging;
using Polochon.Mediation;
using Polochon.Modules;

namespace Polochon
{
    public static class ServiceRegister
    {
        public static IServiceCollection AddPolochon(this IServiceCollection services)
        {
            return services
                .AddPolochonLogging()
                .AddSingleton<IPolochonDispatcher, PolochonRouter>()
                .AddHostedService<LifecycleService>();
        }
    }
}