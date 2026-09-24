namespace Polochon.Abstractions.Telemetry
{
    public class TelemetryOptions
    {
        /// <summary>
        /// Gets or sets the domain/service name for the ActivitySource.
        /// Default: "global"
        /// </summary>
        public string DomainName { get; set; } = "global";

        /// <summary>
        /// Gets or sets the Azure Monitor connection string.
        /// Format: InstrumentationKey=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of additional ActivitySource names to listen to.
        /// </summary>
        public IList<string> AdditionalActivitySources { get; set; } =
        [
            "Microsoft.EntityFrameworkCore",
         "System.Net.Http",
         "Azure.*"
        ];

        /// <summary>
        /// Gets or sets whether console exporter is enabled.
        /// Default: true
        /// </summary>
        public bool EnableConsoleExporter { get; set; } = true;

        /// <summary>
        /// Gets or sets whether Azure Monitor exporter is enabled.
        /// Default: true
        /// </summary>
        public bool EnableAzureMonitorExporter { get; set; } = true;

        /// <summary>
        /// Gets or sets the sampling rate (0.0 to 1.0).
        /// 1.0 = all traces sampled, 0.5 = 50%, etc.
        /// Default: 1.0 (all traces)
        /// </summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets whether metrics instrumentation is enabled.
        /// Default: true
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets whether logging instrumentation is enabled.
        /// Default: true
        /// </summary>
        public bool EnableLogging { get; set; } = true;
    }
}