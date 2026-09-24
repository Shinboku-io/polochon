using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Modules;
using Polochon.Modules;

namespace Polochon.Tests.TestModule
{
    /// <summary>
    /// Test module that inherits from ModuleBase.
    /// This demonstrates how to create a module in the modular monolith architecture.
    /// </summary>
    public sealed class TestModule : ModuleBase
    {
        private static readonly Lazy<TestModule> LazyInstance = new(() => new TestModule());

        /// <summary>
        /// Initializes a new instance of the <see cref="TestModule"/> class.
        /// </summary>
        public TestModule()
            : base("TestModule", [typeof(TestModule).Assembly])
        {
        }

        /// <summary>
        /// Gets the singleton instance of the TestModule.
        /// </summary>
        public static IModularModule Instance => LazyInstance.Value;

        /// <summary>
        /// Configures additional services for the test module.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        protected override void ConfigureAdditionalServices(IServiceCollection services)
        {
            // Handlers are registered via source generation with [MediatorService]
            // No need to manually register them here
        }
    }
}
