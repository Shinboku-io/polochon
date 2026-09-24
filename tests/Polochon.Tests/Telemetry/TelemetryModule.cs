using Polochon.Modules;

namespace Polochon.Tests.Telemetry
{
    /// <summary>
    /// Module used by telemetry tests. Each test class gives it its own name, hence its own activity
    /// source name, so test classes running in parallel never observe each other's activities.
    /// </summary>
    public sealed class TelemetryModule : ModuleBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryModule"/> class.
        /// </summary>
        /// <param name="moduleName">A module name unique to the calling test class.</param>
        public TelemetryModule(string moduleName)
            : base(moduleName, [typeof(TelemetryModule).Assembly])
        {
        }
    }
}
