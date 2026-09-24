using System.Diagnostics;
using System.Diagnostics.Metrics;
using Polochon.Abstractions.Telemetry;

namespace Polochon.Telemetry
{
    /// <summary>
    /// Default <see cref="IModuleTelemetry"/>: an activity source and a meter named after the module.
    /// A singleton of the module's container, disposed with it.
    /// </summary>
    internal sealed class ModuleTelemetry : IModuleTelemetry, IDisposable
    {
        public ModuleTelemetry(string moduleName, IMeterFactory meterFactory)
        {
            var sourceName = PolochonTelemetry.GetModuleSourceName(moduleName);

            ModuleName = moduleName;
            ActivitySource = new ActivitySource(sourceName);

            // From the container's IMeterFactory rather than new Meter(): the factory owns and disposes
            // it, and lets tests or tools observe this container's meters specifically.
            Meter = meterFactory.Create(new MeterOptions(sourceName));
        }

        public string ModuleName { get; }

        public ActivitySource ActivitySource { get; }

        public Meter Meter { get; }

        public void Dispose() => ActivitySource.Dispose();
    }
}
