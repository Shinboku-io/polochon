using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Polochon.Abstractions.Modules;
using Serilog;
using Serilog.Events;

namespace Polochon.Serilog
{
    /// <summary>
    /// Provides extension methods for replacing Polochon's default logging with Serilog.
    /// </summary>
    public static class ServiceRegister
    {
        private const string DefaultOutputTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Replaces Polochon's default Microsoft.Extensions.Logging configuration with Serilog,
        /// using Polochon's base Serilog configuration. Chain this after <c>AddPolochon()</c>,
        /// e.g. <c>services.AddPolochon().WithSerilog()</c>. The resulting logger also becomes
        /// the global <see cref="Log.Logger"/>.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        public static IServiceCollection WithSerilog(this IServiceCollection services)
            => services.WithSerilog(static _ => { });

        /// <summary>
        /// Replaces Polochon's default Microsoft.Extensions.Logging configuration with Serilog.
        /// Polochon's base configuration is applied first, then <paramref name="configureLogger"/>
        /// is invoked so consumers can override or extend it. The resulting logger also becomes
        /// the global <see cref="Log.Logger"/>.
        /// </summary>
        /// <remarks>
        /// Events also reach every other <see cref="ILoggerProvider"/> registered in the container
        /// (e.g. OpenTelemetry's), except Polochon's base console provider, which is removed: Serilog's
        /// own console sink replaces it.
        /// </remarks>
        /// <param name="services">The service collection to register with.</param>
        /// <param name="configureLogger">A callback used to customize the Serilog configuration.</param>
        public static IServiceCollection WithSerilog(this IServiceCollection services, Action<LoggerConfiguration> configureLogger)
        {
            ArgumentNullException.ThrowIfNull(configureLogger);

            RemoveBaseConsoleProvider(services);
            return services.AddSerilog(
                (serviceProvider, loggerConfiguration) =>
                {
                    ApplyBaseConfiguration(serviceProvider, loggerConfiguration);
                    configureLogger(loggerConfiguration);
                },
                writeToProviders: true);
        }

        /// <summary>
        /// Switches a module's logging from Polochon's base console provider to Serilog, using
        /// Polochon's base Serilog configuration. Chain this after a module registration, e.g.
        /// <c>services.AddModule&lt;TModule&gt;().WithSerilog()</c>.
        /// </summary>
        /// <typeparam name="TModule">The concrete module type the builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        public static IModularModuleBuilder<TModule> WithSerilog<TModule>(this IModularModuleBuilder<TModule> builder)
            where TModule : IModularModule
            => builder.WithSerilog(static _ => { });

        /// <summary>
        /// Switches a module's logging from Polochon's base console provider to Serilog.
        /// Polochon's base configuration is applied first - including tagging every log event with
        /// the module's own <c>Name</c> - then <paramref name="configureLogger"/> is invoked so
        /// consumers can override or extend it. Unlike the <see cref="IServiceCollection"/> overload,
        /// this does not touch the global <see cref="Log.Logger"/>: each module gets its own
        /// independent Serilog logger instance, so multiple modules configuring Serilog differently do
        /// not interfere with one another.
        /// </summary>
        /// <remarks>
        /// Events also reach every other <see cref="ILoggerProvider"/> registered in the module's container
        /// (e.g. OpenTelemetry's, through its module-level <c>WithOpenTelemetry()</c>), except Polochon's base
        /// console provider, which is removed: Serilog's own console sink replaces it.
        /// </remarks>
        /// <typeparam name="TModule">The concrete module type the builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="configureLogger">A callback used to customize the module's Serilog configuration.</param>
        public static IModularModuleBuilder<TModule> WithSerilog<TModule>(this IModularModuleBuilder<TModule> builder, Action<LoggerConfiguration> configureLogger)
            where TModule : IModularModule
        {
            ArgumentNullException.ThrowIfNull(configureLogger);

            return builder.ConfigureModule((services, module, hostServices) =>
            {
                RemoveBaseConsoleProvider(services);
                _ = services.AddSerilog(
                    (serviceProvider, loggerConfiguration) =>
                    {
                        ApplyBaseConfiguration(serviceProvider, loggerConfiguration);
                        _ = loggerConfiguration.Enrich.WithProperty("Module", module.Name);
                        configureLogger(loggerConfiguration);
                    },
                    preserveStaticLogger: true,
                    writeToProviders: true);
            });
        }

        /// <summary>
        /// Removes the console provider Polochon's base logging registers: with <c>writeToProviders</c>,
        /// Serilog would otherwise hand it every event its own console sink already writes.
        /// </summary>
        private static void RemoveBaseConsoleProvider(IServiceCollection services)
        {
            for (var index = services.Count - 1; index >= 0; index--)
            {
                if (services[index].ServiceType == typeof(ILoggerProvider) && services[index].ImplementationType == typeof(ConsoleLoggerProvider))
                {
                    services.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Polochon's base Serilog configuration: reads enrichers/sinks/filters registered in the
        /// given container (making the result container-dependent - a module reads its own isolated
        /// container, the host reads its own), enriches from the ambient log context, defaults to
        /// Debug while quieting known-noisy framework namespaces, and writes to the console.
        /// </summary>
        private static void ApplyBaseConfiguration(IServiceProvider serviceProvider, LoggerConfiguration loggerConfiguration)
        {
            loggerConfiguration
                .ReadFrom.Services(serviceProvider)
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Azure", LogEventLevel.Warning)
                .WriteTo.Console(outputTemplate: DefaultOutputTemplate, formatProvider: CultureInfo.InvariantCulture);
        }
    }
}
