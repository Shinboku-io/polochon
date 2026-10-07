using Polochon.Modules;

namespace Polochon.Telemetry.AzureMonitor.Tests
{
    /// <summary>
    /// Minimal <see cref="ModuleBase"/> whose telemetry the tests export.
    /// </summary>
    public sealed class FakeModule : ModuleBase
    {
        /// <summary>The module name.</summary>
        public const string ModuleName = "azmon-fake";

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeModule"/> class.
        /// </summary>
        public FakeModule()
            : base(ModuleName, [typeof(FakeModule).Assembly])
        {
        }
    }
}
