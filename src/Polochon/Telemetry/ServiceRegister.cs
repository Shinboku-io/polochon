using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Polochon.Abstractions.Telemetry;
using Polochon.Mediation;

namespace Polochon.Telemetry
{
    /// <summary>
    /// Provides Polochon's default telemetry: a module's instruments, and tracing plus metrics of
    /// every command and query it handles. Exporter-agnostic - an exporter (e.g. OpenTelemetry) is
    /// configured once on the host and collects every module's telemetry by name.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers the module's <see cref="IModuleTelemetry"/> (activity source and meter named
        /// <c>Polochon.Modules.{moduleName}</c>) and the pipeline behavior tracing and timing every
        /// command and query. Called automatically by <c>ModuleBase</c>, first, so the behavior runs
        /// outermost.
        /// </summary>
        /// <param name="services">The module's service collection.</param>
        /// <param name="moduleName">The name of the module.</param>
        public static IServiceCollection AddPolochonTelemetry(this IServiceCollection services, string moduleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            _ = services.AddMetrics();
            _ = services.AddSingleton<IModuleTelemetry>(sp => new ModuleTelemetry(moduleName, sp.GetRequiredService<IMeterFactory>()));

            return services.AddPipelineBehavior(typeof(DispatchTelemetryBehavior<,>));
        }
    }
}
