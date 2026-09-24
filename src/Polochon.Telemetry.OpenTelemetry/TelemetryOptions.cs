using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Polochon.Telemetry.OpenTelemetry
{
    /// <summary>
    /// Configures the host's OpenTelemetry pipeline: what is collected, how it is sampled and where it
    /// is exported. Host plumbing only - modules emit through their own <c>IModuleTelemetry</c> and never
    /// see any of it.
    /// </summary>
    public sealed class TelemetryOptions
    {
        /// <summary>
        /// Gets or sets the service name exported with every signal. When <see langword="null"/>,
        /// OpenTelemetry's own resolution applies (the <c>OTEL_SERVICE_NAME</c> environment variable, then
        /// a default based on the process name).
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the activity sources traced besides every Polochon module's
        /// (<c>Polochon.Modules.*</c>, always included). Wildcards are supported. The defaults cover
        /// incoming ASP.NET Core requests - the root of most traces - and common outgoing dependencies.
        /// </summary>
        public IList<string> AdditionalActivitySources { get; set; } =
        [
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "System.Net.Http",
            "Azure.*",
        ];

        /// <summary>
        /// Gets or sets the meters collected besides every Polochon module's (<c>Polochon.Modules.*</c>,
        /// always included). Wildcards are supported. The defaults are .NET's built-in request, HTTP
        /// client and runtime metrics.
        /// </summary>
        public IList<string> AdditionalMeters { get; set; } =
        [
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "System.Net.Http",
            "System.Runtime",
        ];

        /// <summary>
        /// Gets or sets the ratio of traces sampled, from 0.0 (none) to 1.0 (all, the default). Applies to
        /// traces started in this process; a trace started upstream keeps its caller's sampling decision.
        /// </summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the OTLP collector endpoint every signal is exported to, e.g.
        /// <c>http://localhost:4317</c>. When <see langword="null"/>, OTLP export is enabled only if the
        /// standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable is set (as .NET Aspire does),
        /// and then fully configured by the standard <c>OTEL_EXPORTER_OTLP_*</c> variables.
        /// </summary>
        public Uri? OtlpEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the protocol used with <see cref="OtlpEndpoint"/>. Defaults to gRPC (port 4317);
        /// use <see cref="OtlpExportProtocol.HttpProtobuf"/> for port 4318.
        /// </summary>
        public OtlpExportProtocol OtlpProtocol { get; set; } = OtlpExportProtocol.Grpc;

        /// <summary>
        /// Gets or sets a value indicating whether every signal is also written to the console. Defaults to
        /// <see langword="false"/>: useful while developing, far too verbose in production.
        /// </summary>
        public bool EnableConsoleExporter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether metrics are collected. Defaults to <see langword="true"/>.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether logs are exported. Defaults to <see langword="true"/>.
        /// Covers the host's own logs; a module's logs are exported once it is registered with
        /// <c>WithOpenTelemetry()</c> too.
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets an escape hatch applied last to the tracer provider, e.g. to add an instrumentation
        /// library or another exporter.
        /// </summary>
        public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }

        /// <summary>
        /// Gets or sets an escape hatch applied last to the meter provider (when <see cref="EnableMetrics"/>).
        /// </summary>
        public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }

        /// <summary>
        /// Gets or sets an escape hatch applied last to the logger provider (when <see cref="EnableLogging"/>).
        /// </summary>
        public Action<LoggerProviderBuilder>? ConfigureLogging { get; set; }
    }
}
