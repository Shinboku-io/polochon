using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polochon.Abstractions.Modules;
using Polochon.Modules;
using Xunit;
using TestModuleType = Polochon.Tests.TestModule.TestModule;

namespace Polochon.Tests.Modules
{
    /// <summary>
    /// Tests for <see cref="Polochon.Modules.ServiceRegister.AddModule{TModule}(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        /// <summary>
        /// Tests that AddModule registers the module as the resolvable IModularModule singleton.
        /// </summary>
        [Fact]
        public void AddModule_RegistersModuleAsIModularModuleSingleton()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.AddModule<TestModuleType>();
            using var provider = services.BuildServiceProvider();

            // Assert
            var module = provider.GetRequiredService<IModularModule>();
            Assert.IsType<TestModuleType>(module);
            Assert.Equal("TestModule", module.Name);
        }

        /// <summary>
        /// Tests that a callback queued via the returned builder's ConfigureModule is applied to the
        /// module's own isolated service collection before it is initialized - this is the seam Polochon
        /// extension packages (e.g. Polochon.Serilog) use to layer configuration onto a module.
        /// </summary>
        [Fact]
        public async Task AddModule_ConfigureModule_AppliesCallbackToModuleContainer()
        {
            // Arrange
            var services = new ServiceCollection();
            var marker = new object();

            var builder = services.AddModule<TestModuleType>();
            _ = builder.ConfigureModule((moduleServices, _, _) => moduleServices.AddSingleton(marker));

            using var provider = services.BuildServiceProvider();
            var module = (TestModuleType)provider.GetRequiredService<IModularModule>();

            // Act
            await module.InitializeAsync();

            // Assert
            Assert.Same(marker, module.GetRequiredService<object>());
        }

        /// <summary>
        /// Tests that ConfigureModule's callback receives the module instance with its Name already
        /// set, so extension packages (e.g. Polochon.Serilog) can tag what they configure with it
        /// automatically instead of requiring the caller to repeat it.
        /// </summary>
        [Fact]
        public async Task AddModule_ConfigureModule_ReceivesModuleWithName()
        {
            // Arrange
            var services = new ServiceCollection();
            string? observedName = null;

            var builder = services.AddModule<TestModuleType>();
            _ = builder.ConfigureModule((_, configuredModule, _) => observedName = configuredModule.Name);

            using var provider = services.BuildServiceProvider();
            var module = (TestModuleType)provider.GetRequiredService<IModularModule>();

            // Act
            await module.InitializeAsync();

            // Assert
            Assert.Equal("TestModule", observedName);
        }

        /// <summary>
        /// Tests that ConfigureModule's callback receives the host's root provider, so extensions can
        /// bridge a host-owned service into the module's isolated container (e.g. WithFeatureManagement
        /// forwarding the host's feature definitions).
        /// </summary>
        [Fact(DisplayName = "ConfigureModule hands the callback the host's root service provider")]
        public async Task AddModuleConfigureModuleReceivesHostRootProvider()
        {
            // Arrange
            var services = new ServiceCollection();
            var hostMarker = new object();
            _ = services.AddSingleton(hostMarker);

            var builder = services.AddModule<TestModuleType>();
            _ = builder.ConfigureModule((moduleServices, _, hostServices) => moduleServices.AddSingleton(new HostMarker(hostServices.GetRequiredService<object>())));

            using var provider = services.BuildServiceProvider();
            var module = (TestModuleType)provider.GetRequiredService<IModularModule>();

            // Act
            await module.InitializeAsync();

            // Assert
            Assert.Same(hostMarker, module.GetRequiredService<HostMarker>().Value);
        }

        /// <summary>
        /// Tests that ConfigureModule returns the same builder instance, so extension methods can chain
        /// (e.g. AddModule&lt;TModule&gt;().WithSerilog().SomeOtherExtension()).
        /// </summary>
        [Fact]
        public void ConfigureModule_ReturnsSameBuilderInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = services.AddModule<TestModuleType>();

            // Act
            var result = builder.ConfigureModule(static (_, _, _) => { });

            // Assert
            Assert.Same(builder, result);
        }

        /// <summary>
        /// Tests that ConfigureModule, being strongly typed on TModule, hands the callback the actual module instance (not just its Name), so callers can read
        /// module-specific properties or call module-specific methods on the real, concrete module type.
        /// </summary>
        [Fact]
        public async Task AddModule_TypedConfigureModule_ReceivesActualModuleInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            TestModuleType? observedModule = null;

            var builder = services.AddModule<TestModuleType>();
            _ = builder.ConfigureModule((IServiceCollection _, TestModuleType module, IServiceProvider _) => observedModule = module);

            using var provider = services.BuildServiceProvider();
            var module = (TestModuleType)provider.GetRequiredService<IModularModule>();

            // Act
            await module.InitializeAsync();

            // Assert
            Assert.Same(module, observedModule);
            Assert.Equal("TestModule", observedModule!.Name);
        }

        /// <summary>
        /// Tests that WithOptions makes the host-provided settings instance resolvable from the module's
        /// isolated container, both directly and as IOptions&lt;T&gt;.
        /// </summary>
        [Fact(DisplayName = "WithOptions exposes the settings to the module as TOptions and IOptions<TOptions>")]
        public async Task WithOptionsExposesSettingsToModule()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new SampleOptions { Value = Guid.NewGuid().ToString() };

            _ = services.AddModule<TestModuleType>().WithOptions(settings);

            using var provider = services.BuildServiceProvider();
            var module = (TestModuleType)provider.GetRequiredService<IModularModule>();

            // Act
            await module.InitializeAsync();

            // Assert
            Assert.Same(settings, module.GetRequiredService<SampleOptions>());
            Assert.Same(settings, module.GetRequiredService<IOptions<SampleOptions>>().Value);
        }

        /// <summary>
        /// Settings type used by the WithOptions test.
        /// </summary>
        private sealed class SampleOptions
        {
            public string? Value { get; init; }
        }

        /// <summary>
        /// Wraps a host service registered into the module container, so tests can tell it apart.
        /// </summary>
        private sealed class HostMarker
        {
            public HostMarker(object value)
            {
                Value = value;
            }

            public object Value { get; }
        }
    }
}
