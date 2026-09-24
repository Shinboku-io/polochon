using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Polochon.Abstractions.Modules;
using Polochon.FeatureManagement;
using Polochon.Modules;
using Xunit;
using TestModuleType = Polochon.Tests.TestModule.TestModule;

namespace Polochon.Tests.FeatureManagement
{
    /// <summary>
    /// Tests for <see cref="Polochon.FeatureManagement.ServiceRegister"/>: a module evaluates feature
    /// flags whose definitions are owned by the host, without the host's configuration reaching it.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        /// <summary>
        /// Tests that a module resolves a flag's state from the definitions configured on the host.
        /// </summary>
        [Fact(DisplayName = "WithFeatureManagement lets the module evaluate flags defined on the host")]
        public async Task WithFeatureManagementModuleEvaluatesFlagsDefinedOnHost()
        {
            // Arrange
            var configuration = BuildConfiguration(new() { ["FeatureManagement:Beta"] = "true", ["FeatureManagement:Legacy"] = "false" });
            await using var host = BuildHost(configuration, builder => builder.WithFeatureManagement());
            var module = await InitializeModuleAsync(host);

            // Act
            var featureManager = module.GetRequiredService<IFeatureManager>();

            // Assert
            Assert.True(await featureManager.IsEnabledAsync("Beta"));
            Assert.False(await featureManager.IsEnabledAsync("Legacy"));
            Assert.False(await featureManager.IsEnabledAsync("Unknown"));
        }

        /// <summary>
        /// Tests that a change to the host's definitions (e.g. an Azure App Configuration refresh, or
        /// an appsettings reload) is seen by the module without restarting it.
        /// </summary>
        [Fact(DisplayName = "WithFeatureManagement lets the module see a reload of the host's definitions")]
        public async Task WithFeatureManagementModuleSeesHostDefinitionReload()
        {
            // Arrange
            var configuration = BuildConfiguration(new() { ["FeatureManagement:Beta"] = "false" });
            await using var host = BuildHost(configuration, builder => builder.WithFeatureManagement());
            var module = await InitializeModuleAsync(host);
            var featureManager = module.GetRequiredService<IFeatureManager>();
            Assert.False(await featureManager.IsEnabledAsync("Beta"));

            // Act
            configuration["FeatureManagement:Beta"] = "true";
            configuration.Reload();

            // Assert
            Assert.True(await featureManager.IsEnabledAsync("Beta"));
        }

        /// <summary>
        /// Tests that the module container gets feature definitions, not the host's configuration:
        /// the source of those definitions (and its plumbing) stays a host concern.
        /// </summary>
        [Fact(DisplayName = "WithFeatureManagement does not expose the host's configuration to the module")]
        public async Task WithFeatureManagementDoesNotExposeHostConfigurationToModule()
        {
            // Arrange
            var configuration = BuildConfiguration(new() { ["FeatureManagement:Beta"] = "true" });
            await using var host = BuildHost(configuration, builder => builder.WithFeatureManagement());

            // Act
            var module = await InitializeModuleAsync(host);

            // Assert
            Assert.Null(module.GetService<IConfiguration>());
            Assert.IsNotType<ConfigurationFeatureDefinitionProvider>(module.GetRequiredService<IFeatureDefinitionProvider>());
        }

        /// <summary>
        /// Tests that feature filters are evaluated inside the module: a filter registered through the
        /// configure callback only exists in the module, yet gates a flag defined on the host.
        /// </summary>
        [Fact(DisplayName = "WithFeatureManagement configure callback registers a feature filter in the module")]
        public async Task WithFeatureManagementConfigureCallbackRegistersModuleFeatureFilter()
        {
            // Arrange
            var configuration = BuildConfiguration(new() { ["FeatureManagement:Gated:EnabledFor:0:Name"] = AlwaysOnFilter.Alias });
            await using var host = BuildHost(configuration, builder => builder.WithFeatureManagement(featureManagement => featureManagement.AddFeatureFilter<AlwaysOnFilter>()));
            var module = await InitializeModuleAsync(host);

            // Act
            var enabled = await module.GetRequiredService<IFeatureManager>().IsEnabledAsync("Gated");

            // Assert
            Assert.True(enabled);
        }

        /// <summary>
        /// Tests that a module asking for feature management on a host that has none fails at module
        /// initialization (i.e. at startup), naming the module, rather than on first flag evaluation.
        /// </summary>
        [Fact(DisplayName = "WithFeatureManagement fails at module initialization when the host has no feature management")]
        public async Task WithFeatureManagementWithoutHostFeatureManagementFailsAtInitialization()
        {
            // Arrange
            var services = new ServiceCollection();
            _ = services.AddModule<TestModuleType>().WithFeatureManagement();
            await using var host = services.BuildServiceProvider();
            var module = (TestModuleType)host.GetRequiredService<IModularModule>();

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => module.InitializeAsync());

            // Assert
            Assert.Contains("TestModule", exception.Message, StringComparison.Ordinal);
        }

        private static IConfigurationRoot BuildConfiguration(Dictionary<string, string?> values)
            => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        private static ServiceProvider BuildHost(IConfiguration configuration, Action<IModularModuleBuilder<TestModuleType>> configureModule)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(configuration);
            _ = services.AddFeatureManagement();
            configureModule(services.AddModule<TestModuleType>());

            return services.BuildServiceProvider();
        }

        private static async Task<TestModuleType> InitializeModuleAsync(ServiceProvider host)
        {
            var module = (TestModuleType)host.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            return module;
        }

        /// <summary>
        /// A feature filter enabling any feature it gates, only ever registered in the module.
        /// </summary>
        [FilterAlias(Alias)]
        private sealed class AlwaysOnFilter : IFeatureFilter
        {
            public const string Alias = "AlwaysOn";

            public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) => Task.FromResult(true);
        }
    }
}
