using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Telemetry.OpenTelemetry;

namespace Polochon.Telemetry.AzureMonitor
{
    /// <summary>
    /// Adds Azure Monitor (Application Insights) as an exporter of the host's Polochon OpenTelemetry
    /// pipeline. Host-level only, like every exporter: modules never see the connection string.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Exports the host's traces - and its metrics and logs, when <c>WithOpenTelemetry()</c> collects
        /// them - to Azure Monitor, using the <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> environment
        /// variable. Chain this after <c>WithOpenTelemetry()</c>, e.g.
        /// <c>services.AddPolochon().WithOpenTelemetry().WithAzureMonitor()</c>.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection WithAzureMonitor(this IServiceCollection services)
            => services.WithAzureMonitor(static _ => { });

        /// <summary>
        /// Exports the host's traces - and its metrics and logs, when <c>WithOpenTelemetry()</c> collects
        /// them - to Azure Monitor. Chain this after <c>WithOpenTelemetry()</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An exporter is added to each signal the host collects, and only those: a signal the host turned
        /// off stays off, which is why the per-signal exporters are used rather than Azure Monitor's
        /// <c>UseAzureMonitorExporter()</c>, which switches every signal on.
        /// </para>
        /// <para>
        /// Azure Monitor's trace exporter installs its own sampler (rate-limited by default), replacing the
        /// host's. It is therefore given <see cref="TelemetryOptions.SamplingRate"/> as its
        /// <see cref="AzureMonitorExporterOptions.SamplingRatio"/>, rate limiting off, before
        /// <paramref name="configure"/> runs: the host's rate still decides, and Application Insights records
        /// it to extrapolate counts. Set <see cref="AzureMonitorExporterOptions.TracesPerSecond"/> in
        /// <paramref name="configure"/> to opt into rate-limited sampling instead.
        /// </para>
        /// </remarks>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configure">
        /// A callback connecting to Application Insights: <see cref="AzureMonitorExporterOptions.ConnectionString"/>
        /// (defaults to the <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> environment variable),
        /// <see cref="AzureMonitorExporterOptions.Credential"/> for Microsoft Entra authentication, offline storage...
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><c>WithOpenTelemetry()</c> has not been called on these services before.</exception>
        public static IServiceCollection WithAzureMonitor(this IServiceCollection services, Action<AzureMonitorExporterOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var telemetry = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TelemetryOptions))?.ImplementationInstance as TelemetryOptions
                ?? throw new InvalidOperationException(
                    "WithAzureMonitor() adds an exporter to the host's OpenTelemetry pipeline: call WithOpenTelemetry() first, " +
                    "e.g. services.AddPolochon().WithOpenTelemetry().WithAzureMonitor().");

            void ConfigureExporter(AzureMonitorExporterOptions exporter)
            {
                exporter.SamplingRatio = (float)telemetry.SamplingRate;
                exporter.TracesPerSecond = null;
                configure(exporter);
            }

            var openTelemetry = services.AddOpenTelemetry();
            _ = openTelemetry.WithTracing(tracing => tracing.AddAzureMonitorTraceExporter(ConfigureExporter));

            if (telemetry.EnableMetrics)
            {
                _ = openTelemetry.WithMetrics(metrics => metrics.AddAzureMonitorMetricExporter(ConfigureExporter));
            }

            if (telemetry.EnableLogging)
            {
                _ = openTelemetry.WithLogging(logging => logging.AddAzureMonitorLogExporter(ConfigureExporter));
            }

            return services;
        }
    }
}
