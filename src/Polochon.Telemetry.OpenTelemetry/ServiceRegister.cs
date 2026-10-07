using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polochon.Abstractions.Modules;
using Polochon.Abstractions.Telemetry;

namespace Polochon.Telemetry.OpenTelemetry
{
    /// <summary>
    /// Exports the telemetry of a Polochon host and its modules through OpenTelemetry. Traces and
    /// metrics only need the host-level <c>WithOpenTelemetry()</c>: module activity sources and meters
    /// are collected by name. Logs also need the module-level <c>WithOpenTelemetry()</c>, since every
    /// module has its own logger factory.
    /// </summary>
    public static class ServiceRegister
    {
        private const string OtlpEndpointEnvironmentVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

        /// <summary>
        /// Sets up the host's OpenTelemetry pipeline with Polochon's defaults: every module's traces and
        /// metrics, the host's logs, and OTLP export when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
        /// Chain this after <c>AddPolochon()</c>, e.g. <c>services.AddPolochon().WithOpenTelemetry()</c>.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection WithOpenTelemetry(this IServiceCollection services)
            => services.WithOpenTelemetry(static _ => { });

        /// <summary>
        /// Sets up the host's OpenTelemetry pipeline: every module's traces and metrics (<c>Polochon.Modules.*</c>)
        /// plus <see cref="TelemetryOptions.AdditionalActivitySources"/> and <see cref="TelemetryOptions.AdditionalMeters"/>,
        /// the host's logs, sampling, and the configured exporters. Chain this after <c>AddPolochon()</c>.
        /// Other packages (e.g. Polochon.Telemetry.AzureMonitor) add exporters to the same pipeline.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configure">A callback tuning what is collected and where it is exported.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="TelemetryOptions.SamplingRate"/> is outside 0.0-1.0.</exception>
        public static IServiceCollection WithOpenTelemetry(this IServiceCollection services, Action<TelemetryOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new TelemetryOptions();
            configure(options);
            ArgumentOutOfRangeException.ThrowIfLessThan(options.SamplingRate, 0.0, $"{nameof(TelemetryOptions)}.{nameof(TelemetryOptions.SamplingRate)}");
            ArgumentOutOfRangeException.ThrowIfGreaterThan(options.SamplingRate, 1.0, $"{nameof(TelemetryOptions)}.{nameof(TelemetryOptions.SamplingRate)}");

            // Registered so exporter packages (e.g. Polochon.Telemetry.AzureMonitor) chained after this call
            // only add exporters to the signals this host actually collects.
            _ = services.AddSingleton(options);

            var openTelemetry = services.AddOpenTelemetry();

            if (!string.IsNullOrWhiteSpace(options.ServiceName))
            {
                _ = openTelemetry.ConfigureResource(resource => resource.AddService(options.ServiceName));
            }

            _ = openTelemetry.WithTracing(tracing => ConfigureTracing(tracing, options));

            if (options.EnableMetrics)
            {
                _ = openTelemetry.WithMetrics(metrics => ConfigureMetrics(metrics, options));
            }

            if (options.EnableLogging)
            {
                _ = openTelemetry.WithLogging(
                    logging => ConfigureLogging(logging, options),
                    logger =>
                    {
                        // Scopes carry the module name of module logs (polochon.module), among others.
                        logger.IncludeScopes = true;
                        logger.IncludeFormattedMessage = true;
                    });
            }

            if (options.OtlpEndpoint is not null)
            {
                _ = openTelemetry.UseOtlpExporter(options.OtlpProtocol, options.OtlpEndpoint);
            }
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OtlpEndpointEnvironmentVariable)))
            {
                _ = openTelemetry.UseOtlpExporter();
            }

            return services;
        }

        /// <summary>
        /// Exports this module's logs through the host's OpenTelemetry pipeline, each entry tagged with the
        /// module's name (<c>polochon.module</c>). Its traces and metrics need nothing here: the host's
        /// <c>WithOpenTelemetry()</c> collects them already. Works alongside <c>WithSerilog()</c>, in either
        /// order. Requires the host to export logs through OpenTelemetry, e.g.
        /// <c>services.AddPolochon().WithOpenTelemetry()</c>; module initialization fails otherwise.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IModularModuleBuilder<TModule> WithOpenTelemetry<TModule>(this IModularModuleBuilder<TModule> builder)
            where TModule : IModularModule
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.ConfigureModule((services, module, hostServices) =>
            {
                // Resolved now, at module initialization: a host without OpenTelemetry logging fails at
                // startup instead of silently dropping every module log.
                var hostProvider = hostServices.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>().FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Module '{module.Name}' exports its logs through OpenTelemetry, but the host has no OpenTelemetry logging. " +
                        "Register it on the host (e.g. services.AddPolochon().WithOpenTelemetry(), with EnableLogging left on).");

                _ = services.AddSingleton<ILoggerProvider>(new HostLoggerProvider(hostProvider, module.Name));
            });
        }

        private static void ConfigureTracing(TracerProviderBuilder tracing, TelemetryOptions options)
        {
            _ = tracing
                .AddSource(PolochonTelemetry.AllModulesSourceName)
                .AddSource([.. options.AdditionalActivitySources])
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRate)));

            if (options.EnableConsoleExporter)
            {
                _ = tracing.AddConsoleExporter();
            }

            options.ConfigureTracing?.Invoke(tracing);
        }

        private static void ConfigureMetrics(MeterProviderBuilder metrics, TelemetryOptions options)
        {
            _ = metrics
                .AddMeter(PolochonTelemetry.AllModulesSourceName)
                .AddMeter([.. options.AdditionalMeters]);

            if (options.EnableConsoleExporter)
            {
                _ = metrics.AddConsoleExporter();
            }

            options.ConfigureMetrics?.Invoke(metrics);
        }

        private static void ConfigureLogging(LoggerProviderBuilder logging, TelemetryOptions options)
        {
            if (options.EnableConsoleExporter)
            {
                _ = logging.AddConsoleExporter();
            }

            options.ConfigureLogging?.Invoke(logging);
        }
    }
}
