using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Polochon.Abstractions.Modules;
using Polochon.Modules;
using Xunit;

namespace Polochon.FeatureManagement.AzureAppConfiguration.Tests
{
    /// <summary>
    /// Tests for <see cref="ServiceRegister"/>. None of them reach a real Azure App Configuration store.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        // Resolves nowhere (.invalid is reserved), so connecting fails immediately.
        private const string UnreachableConnectionString = "Endpoint=https://polochon-test.invalid;Id=test;Secret=dGVzdA==";

        /// <summary>
        /// Tests that registering without any way to reach the store is rejected up front.
        /// </summary>
        [Fact(DisplayName = "AddAzureAppConfigurationFeatureFlags without endpoint or connection string throws")]
        public void AddAzureAppConfigurationFeatureFlagsWithoutEndpointOrConnectionStringThrows()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

            // Act
            var exception = Assert.Throws<ArgumentException>(() => builder.AddAzureAppConfigurationFeatureFlags(static _ => { }));

            // Assert
            Assert.Contains(nameof(AzureAppConfigurationFeatureFlagsOptions.Endpoint), exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Tests that an endpoint and a connection string together are rejected: which one wins would be ambiguous.
        /// </summary>
        [Fact(DisplayName = "AddAzureAppConfigurationFeatureFlags with both endpoint and connection string throws")]
        public void AddAzureAppConfigurationFeatureFlagsWithBothEndpointAndConnectionStringThrows()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

            // Act
            var exception = Record.Exception(() => builder.AddAzureAppConfigurationFeatureFlags(options =>
            {
                options.Endpoint = new Uri("https://polochon-test.invalid");
                options.ConnectionString = UnreachableConnectionString;
            }));

            // Assert
            Assert.IsType<ArgumentException>(exception);
        }

        /// <summary>
        /// Tests that a refresh interval under the provider's own one-second minimum is rejected up front.
        /// </summary>
        [Fact(DisplayName = "AddAzureAppConfigurationFeatureFlags with a sub-second refresh interval throws")]
        public void AddAzureAppConfigurationFeatureFlagsWithSubSecondRefreshIntervalThrows()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

            // Act
            var exception = Assert.Throws<ArgumentException>(() => builder.AddAzureAppConfigurationFeatureFlags(options =>
            {
                options.ConnectionString = UnreachableConnectionString;
                options.RefreshInterval = TimeSpan.FromMilliseconds(500);
            }));

            // Assert
            Assert.Contains(nameof(AzureAppConfigurationFeatureFlagsOptions.RefreshInterval), exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Tests that the host gets feature management, the provider's refresher and the background refresh.
        /// </summary>
        [Fact(DisplayName = "AddFeatureFlagServices registers feature management and background refresh")]
        public void AddFeatureFlagServicesRegistersFeatureManagementAndBackgroundRefresh()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = ServiceRegister.AddFeatureFlagServices(services, new AzureAppConfigurationFeatureFlagsOptions());

            // Assert
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IFeatureManager));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IConfigurationRefresherProvider));
            Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(FeatureFlagRefreshService));
        }

        /// <summary>
        /// Tests that a host which already called AddFeatureManagement() itself (e.g. to add filters) keeps
        /// that registration instead of failing on a second one.
        /// </summary>
        [Fact(DisplayName = "AddFeatureFlagServices keeps feature management the host already registered")]
        public void AddFeatureFlagServicesKeepsFeatureManagementTheHostAlreadyRegistered()
        {
            // Arrange
            var services = new ServiceCollection();
            _ = services.AddFeatureManagement();
            var featureManagerRegistrations = services.Count(descriptor => descriptor.ServiceType == typeof(IFeatureManager));

            // Act
            _ = ServiceRegister.AddFeatureFlagServices(services, new AzureAppConfigurationFeatureFlagsOptions());

            // Assert
            Assert.Equal(featureManagerRegistrations, services.Count(descriptor => descriptor.ServiceType == typeof(IFeatureManager)));
        }

        /// <summary>
        /// Tests the full host-to-module path with an optional, unreachable store: the host still starts,
        /// flags fall back to the host's other configuration sources, and a module evaluates them through
        /// WithFeatureManagement() without knowing about Azure App Configuration at all.
        /// </summary>
        [Fact(DisplayName = "Optional unreachable store falls back to host configuration, seen by modules")]
        public async Task OptionalUnreachableStoreFallsBackToHostConfigurationSeenByModules()
        {
            // Arrange
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["FeatureManagement:Beta"] = "true" });
            _ = builder.Services.AddModule<FakeModule>().WithFeatureManagement();

            // Act
            _ = builder.AddAzureAppConfigurationFeatureFlags(options =>
            {
                options.ConnectionString = UnreachableConnectionString;
                options.Optional = true;
                options.ConfigureProvider = provider => provider.ConfigureStartupOptions(startup => startup.Timeout = TimeSpan.FromSeconds(1));
            });
            using var host = builder.Build();
            var module = (FakeModule)host.Services.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            // Assert
            Assert.Single(host.Services.GetRequiredService<IConfigurationRefresherProvider>().Refreshers);
            Assert.True(await module.GetRequiredService<IFeatureManager>().IsEnabledAsync("Beta"));
        }
    }
}
