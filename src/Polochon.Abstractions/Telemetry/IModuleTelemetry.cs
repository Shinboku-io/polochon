using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Polochon.Abstractions.Telemetry
{
    /// <summary>
    /// A module's own telemetry instruments, registered in every module's isolated container. Emit
    /// through the standard .NET APIs - <see cref="System.Diagnostics.ActivitySource"/> for traces,
    /// <see cref="System.Diagnostics.Metrics.Meter"/> for metrics, <c>ILogger</c> for logs - so any
    /// exporter (OpenTelemetry, Azure Monitor, Aspire dashboard...) collects them without an adapter.
    /// </summary>
    /// <remarks>
    /// Both instruments are named <c>Polochon.Modules.{module name}</c> (see
    /// <see cref="PolochonTelemetry.GetModuleSourceName"/>). Listeners are process-wide, so the host
    /// collects every module's telemetry by name - no exporter configuration reaches a module. When
    /// nothing listens, <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> returns
    /// <see langword="null"/> and measurements are dropped: instrumenting costs next to nothing.
    /// </remarks>
    public interface IModuleTelemetry
    {
        /// <summary>
        /// Gets the name of the module these instruments belong to.
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Gets the module's activity source, for traces.
        /// </summary>
        ActivitySource ActivitySource { get; }

        /// <summary>
        /// Gets the module's meter, for metrics. Owned by the module's container: disposed with it.
        /// </summary>
        Meter Meter { get; }
    }
}
