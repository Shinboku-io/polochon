using Polochon.Modules;

namespace Polochon.Serilog.Tests
{
    /// <summary>
    /// Minimal <see cref="ModuleBase"/> used to exercise <c>AddModule&lt;TModule&gt;().WithSerilog()</c>.
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
