using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Polochon.Abstractions.Modules;
using Polochon.Abstractions.Telemetry;
using Polochon.Modules;
using Polochon.Telemetry.OpenTelemetry;
using Xunit;

namespace Polochon.Telemetry.AzureMonitor.Tests
{
    /// <summary>
    /// Tests for <see cref="ServiceRegister"/>. None of them reach Application Insights: the ingestion
    /// endpoint does not resolve, and offline storage and statsbeat are off.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        private const string UnreachableConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://polochon-test.invalid/";

        /// <summary>
        /// Initializes static members of the <see cref="ServiceRegisterTests"/> class: turns off the
        /// exporter's statsbeat, which otherwise reports to Microsoft's own endpoints.
        /// </summary>
        static ServiceRegisterTests()
        {
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_STATSBEAT_DISABLED", "true");
        }

        /// <summary>
        /// Tests that Azure Monitor cannot be added before the pipeline it exports exists.
        /// </summary>
        [Fact(DisplayName = "WithAzureMonitor before WithOpenTelemetry throws")]
        public void WithAzureMonitorBeforeWithOpenTelemetryThrows()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => services.WithAzureMonitor(ConfigureOffline));

            // Assert
            Assert.Contains("WithOpenTelemetry()", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Tests that the Azure Monitor exporter joins the host's pipeline next to its other exporters:
        /// the host starts, and module traces still reach the other exporter.
        /// </summary>
        [Fact(DisplayName = "WithAzureMonitor exports alongside the pipeline's other exporters")]
        public async Task WithAzureMonitorExportsAlongsideOtherExporters()
        {
            // Arrange
            var activities = new List<Activity>();
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            _ = builder.Services.AddPolochon()
                .WithOpenTelemetry(options => options.ConfigureTracing = tracing => tracing.AddInMemoryExporter(activities))
                .WithAzureMonitor(ConfigureOffline);
            _ = builder.Services.AddModule<FakeModule>();
            using var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);

            // Act
            _ = await host.Services.GetRequiredService<IModularModule>().SendQueryAsync(new ProbeQuery(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(activities, activity => activity.Source.Name == PolochonTelemetry.GetModuleSourceName(FakeModule.ModuleName));
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that the host's sampling rate still decides once Azure Monitor, which brings its own
        /// sampler, joins the pipeline: a zero rate exports no module trace.
        /// </summary>
        [Fact(DisplayName = "WithAzureMonitor keeps the host's sampling rate")]
        public async Task WithAzureMonitorKeepsTheHostsSamplingRate()
        {
            // Arrange
            var activities = new List<Activity>();
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            _ = builder.Services.AddPolochon()
                .WithOpenTelemetry(options =>
                {
                    options.SamplingRate = 0;
                    options.ConfigureTracing = tracing => tracing.AddInMemoryExporter(activities);
                })
                .WithAzureMonitor(ConfigureOffline);
            _ = builder.Services.AddModule<FakeModule>();
            using var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);

            // Act
            _ = await host.Services.GetRequiredService<IModularModule>().SendQueryAsync(new ProbeQuery(), TestContext.Current.CancellationToken);

            // Assert
            Assert.DoesNotContain(activities, activity => activity.Source.Name == PolochonTelemetry.GetModuleSourceName(FakeModule.ModuleName));
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that a signal the host turned off stays off: Azure Monitor does not switch log export on.
        /// </summary>
        [Fact(DisplayName = "WithAzureMonitor keeps a signal the host turned off disabled")]
        public void WithAzureMonitorKeepsSignalTheHostTurnedOffDisabled()
        {
            // Arrange
            var services = new ServiceCollection();
            _ = services.WithOpenTelemetry(options => options.EnableLogging = false);

            // Act
            _ = services.WithAzureMonitor(ConfigureOffline);
            using var provider = services.BuildServiceProvider();

            // Assert
            Assert.DoesNotContain(provider.GetServices<ILoggerProvider>(), loggerProvider => loggerProvider is OpenTelemetryLoggerProvider);
        }

        private static void ConfigureOffline(AzureMonitorExporterOptions options)
        {
            options.ConnectionString = UnreachableConnectionString;
            options.DisableOfflineStorage = true;
        }
    }
}
