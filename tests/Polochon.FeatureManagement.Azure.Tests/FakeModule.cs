using Polochon.Modules;

namespace Polochon.FeatureManagement.Azure.Tests
{
    /// <summary>
    /// Minimal <see cref="ModuleBase"/> used to exercise <c>AddModule&lt;TModule&gt;().WithFeatureManagement()</c>
    /// against a host whose feature definitions come from Azure App Configuration.
    /// </summary>
    public sealed class FakeModule : ModuleBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FakeModule"/> class.
        /// </summary>
        public FakeModule()
            : base("fake", [typeof(FakeModule).Assembly])
        {
        }
    }
}
