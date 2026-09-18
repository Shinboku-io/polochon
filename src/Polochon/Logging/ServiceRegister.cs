using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Polochon.Logging
{
    /// <summary>
    /// Provides Polochon's default logging configuration, based on Microsoft.Extensions.Logging.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers Polochon's base Microsoft.Extensions.Logging pipeline (a console provider).
        /// Called automatically by <c>AddPolochon()</c> so logging works out of the box.
        /// Consumers who want a different logging provider can install a Polochon logging
        /// integration package (e.g. Polochon.Serilog) and chain its extension method
        /// (e.g. <c>WithSerilog()</c>) after <c>AddPolochon()</c> to replace this configuration.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        public static IServiceCollection AddPolochonLogging(this IServiceCollection services)
        {
            _ = services.AddLogging(builder => builder.AddConsole());

            return services;
        }
    }
}
