using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polochon.Abstractions.Modules;
using Polochon.Abstractions.Telemetry;
using Polochon.Modules;
using Polochon.Serilog;
using Xunit;

namespace Polochon.Telemetry.OpenTelemetry.Tests
{
    /// <summary>
    /// Tests for <see cref="ServiceRegister"/>: a real host exporting its modules' telemetry to in-memory
    /// exporters. All tests share one module name, hence live in this one class, which runs them sequentially.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        private static readonly string ModuleSourceName = PolochonTelemetry.GetModuleSourceName(FakeModule.ModuleName);

        /// <summary>
        /// Tests that a command or query handled by a module is exported as a trace from the module's source,
        /// with no module-level registration.
        /// </summary>
        [Fact(DisplayName = "WithOpenTelemetry exports module dispatch traces")]
        public async Task WithOpenTelemetryExportsModuleDispatchTraces()
        {
            // Arrange
            var activities = new List<Activity>();
            using var host = await StartHostAsync(options => options.ConfigureTracing = tracing => tracing.AddInMemoryExporter(activities), module => { });

            // Act
            await SendProbeQueryAsync(host);

            // Assert
            var activity = Assert.Single(activities, activity => activity.Source.Name == ModuleSourceName);
            Assert.Equal(nameof(ProbeQuery), activity.DisplayName);
            Assert.Equal(FakeModule.ModuleName, activity.GetTagItem(PolochonTelemetry.ModuleTag));
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that module metrics are exported, with no module-level registration.
        /// </summary>
        [Fact(DisplayName = "WithOpenTelemetry exports module dispatch metrics")]
        public async Task WithOpenTelemetryExportsModuleDispatchMetrics()
        {
            // Arrange
            var metrics = new List<Metric>();
            using var host = await StartHostAsync(options => options.ConfigureMetrics = meters => meters.AddInMemoryExporter(metrics), module => { });

            // Act
            await SendProbeQueryAsync(host);
            _ = host.Services.GetRequiredService<MeterProvider>().ForceFlush();

            // Assert
            Assert.Contains(metrics, metric => metric.Name == PolochonTelemetry.MessageDurationMetric && metric.MeterName == ModuleSourceName);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that a module registered with WithOpenTelemetry() exports its logs through the host's
        /// pipeline, tagged with the module name.
        /// </summary>
        [Fact(DisplayName = "Module WithOpenTelemetry exports module logs tagged with the module name")]
        public async Task ModuleWithOpenTelemetryExportsModuleLogsTaggedWithModuleName()
        {
            // Arrange
            var logs = new List<LogRecord>();
            using var host = await StartHostAsync(options => options.ConfigureLogging = logging => logging.AddInMemoryExporter(logs), module => module.WithOpenTelemetry());

            // Act
            LogFromModule(host, "otel module event");

            // Assert
            var record = Assert.Single(logs, record => record.FormattedMessage == "otel module event");
            Assert.Equal(FakeModule.ModuleName, GetScopeValue(record, PolochonTelemetry.ModuleTag));
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that a module logging through Serilog still exports its logs through OpenTelemetry - once.
        /// </summary>
        [Fact(DisplayName = "Module WithOpenTelemetry exports the logs of a Serilog module once")]
        public async Task ModuleWithOpenTelemetryExportsSerilogModuleLogsOnce()
        {
            // Arrange
            var logs = new List<LogRecord>();
            using var host = await StartHostAsync(options => options.ConfigureLogging = logging => logging.AddInMemoryExporter(logs), module => module.WithSerilog().WithOpenTelemetry());

            // Act
            LogFromModule(host, "serilog module event");

            // Assert
            var record = Assert.Single(logs, record => record.FormattedMessage == "serilog module event");
            Assert.Equal(FakeModule.ModuleName, GetScopeValue(record, PolochonTelemetry.ModuleTag));
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that a module asking for OpenTelemetry logs on a host that does not export logs fails at
        /// module initialization, naming the module, rather than silently dropping its logs.
        /// </summary>
        [Fact(DisplayName = "Module WithOpenTelemetry fails at initialization when the host exports no logs")]
        public async Task ModuleWithOpenTelemetryFailsAtInitializationWhenHostExportsNoLogs()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            _ = builder.Services.WithOpenTelemetry(options => options.EnableLogging = false);
            _ = builder.Services.AddModule<FakeModule>().WithOpenTelemetry();
            using var host = builder.Build();
            var module = (FakeModule)host.Services.GetRequiredService<IModularModule>();

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => module.InitializeAsync());

            // Assert
            Assert.Contains(FakeModule.ModuleName, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Tests that a sampling rate of zero exports no trace started by the host itself.
        /// </summary>
        [Fact(DisplayName = "WithOpenTelemetry with a zero sampling rate exports no module trace")]
        public async Task WithOpenTelemetryWithZeroSamplingRateExportsNoModuleTrace()
        {
            // Arrange
            var activities = new List<Activity>();
            using var host = await StartHostAsync(
                options =>
                {
                    options.SamplingRate = 0;
                    options.ConfigureTracing = tracing => tracing.AddInMemoryExporter(activities);
                },
                module => { });

            // Act
            await SendProbeQueryAsync(host);

            // Assert
            Assert.DoesNotContain(activities, activity => activity.Source.Name == ModuleSourceName);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that a sampling rate outside 0.0-1.0 is rejected at registration.
        /// </summary>
        [Fact(DisplayName = "WithOpenTelemetry with a sampling rate above one throws")]
        public void WithOpenTelemetryWithSamplingRateAboveOneThrows()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var exception = Record.Exception(() => services.WithOpenTelemetry(options => options.SamplingRate = 1.5));

            // Assert
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }

        private static async Task<IHost> StartHostAsync(Action<TelemetryOptions> configureTelemetry, Action<IModularModuleBuilder<FakeModule>> configureModule)
        {
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            _ = builder.Services.AddPolochon().WithOpenTelemetry(configureTelemetry);
            configureModule(builder.Services.AddModule<FakeModule>());

            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }

        private static async Task SendProbeQueryAsync(IHost host)
            => _ = await host.Services.GetRequiredService<IModularModule>().SendQueryAsync(new ProbeQuery(), TestContext.Current.CancellationToken);

        private static void LogFromModule(IHost host, string message)
        {
            var module = (FakeModule)host.Services.GetRequiredService<IModularModule>();
            var logger = module.GetRequiredService<ILoggerFactory>().CreateLogger<ServiceRegisterTests>();
#pragma warning disable CA2254 // The message is the test's own marker, not a template to reuse.
            logger.LogInformation(message);
#pragma warning restore CA2254
        }

        private static object? GetScopeValue(LogRecord record, string key)
        {
            object? value = null;
            record.ForEachScope(
                (scope, _) =>
                {
                    foreach (var item in scope)
                    {
                        if (item.Key == key)
                        {
                            value = item.Value;
                        }
                    }
                },
                (object?)null);
            return value;
        }
    }
}
