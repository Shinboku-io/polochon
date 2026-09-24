using Polochon.Modules;

namespace Polochon.Telemetry.OpenTelemetry.Tests
{
    /// <summary>
    /// Minimal <see cref="ModuleBase"/> whose telemetry the tests export. Its name gives it an activity
    /// source and meter (<c>Polochon.Modules.otel-fake</c>) no other test assembly uses.
    /// </summary>
    public sealed class FakeModule : ModuleBase
    {
        /// <summary>The module name.</summary>
        public const string ModuleName = "otel-fake";

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeModule"/> class.
        /// </summary>
        public FakeModule()
            : base(ModuleName, [typeof(FakeModule).Assembly])
        {
        }
    }
}
