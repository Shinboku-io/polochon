using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

namespace Polochon.FeatureManagement.Azure
{
    /// <summary>
    /// Makes an Azure App Configuration store the source of the host's feature definitions. Host-level
    /// only: modules keep evaluating flags through <c>WithFeatureManagement()</c>, unaware of where the
    /// definitions come from - no endpoint, credential or Key Vault setting ever reaches them.
    /// </summary>
    public static class ServiceRegister
    {
        // An exact-match key filter no real key uses. Any Select() call stops the provider from loading
        // its default (every key-value with no label), which is how feature flags alone get loaded.
        private const string NoKeyValuesKeyFilter = ".polochon/feature-flags-only";

        /// <summary>
        /// Loads the host's feature flags from Azure App Configuration, keeps them refreshed while the
        /// host runs, and registers feature management on the host (if not already registered) so
        /// modules using <c>WithFeatureManagement()</c> evaluate them. Flags land in the host's
        /// configuration in Microsoft's <c>feature_management</c> schema, next to definitions from other
        /// configuration sources (e.g. appsettings' <c>FeatureManagement</c> section): a flag defined only
        /// elsewhere still works, and a flag defined in both takes the store's value, whatever the source order.
        /// </summary>
        /// <typeparam name="TBuilder">The host builder type, e.g. <c>WebApplicationBuilder</c>.</typeparam>
        /// <param name="builder">The host builder to configure.</param>
        /// <param name="configure">A callback connecting to the store and tuning what is loaded.</param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// Neither or both of <see cref="AzureAppConfigurationFeatureFlagsOptions.Endpoint"/> and
        /// <see cref="AzureAppConfigurationFeatureFlagsOptions.ConnectionString"/> are set, or
        /// <see cref="AzureAppConfigurationFeatureFlagsOptions.RefreshInterval"/> is under one second.
        /// </exception>
        public static TBuilder AddAzureAppConfigurationFeatureFlags<TBuilder>(this TBuilder builder, Action<AzureAppConfigurationFeatureFlagsOptions> configure)
            where TBuilder : IHostApplicationBuilder
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new AzureAppConfigurationFeatureFlagsOptions();
            configure(options);
            Validate(options);

            _ = builder.Configuration.AddAzureAppConfiguration(provider => ConfigureProvider(provider, options), options.Optional);
            _ = AddFeatureFlagServices(builder.Services, options);

            return builder;
        }

        /// <summary>
        /// Registers the host services: the provider's refresher, feature management, and the
        /// background refresh. Split from the configuration source so it can be tested without a store.
        /// </summary>
        internal static IServiceCollection AddFeatureFlagServices(IServiceCollection services, AzureAppConfigurationFeatureFlagsOptions options)
        {
            _ = services.AddAzureAppConfiguration();

            // The host may already have registered feature management itself (e.g. WithFeatureManagement()
            // to add filters): keep that registration rather than adding a second one, which would throw
            // if it is AddScopedFeatureManagement().
            if (!services.Any(descriptor => descriptor.ServiceType == typeof(IFeatureManager)))
            {
                _ = services.AddFeatureManagement();
            }

            services.TryAddSingleton(TimeProvider.System);
            _ = services.AddSingleton(new FeatureFlagRefreshSchedule { Interval = options.RefreshInterval });
            _ = services.AddHostedService<FeatureFlagRefreshService>();

            return services;
        }

        private static void Validate(AzureAppConfigurationFeatureFlagsOptions options)
        {
            var hasEndpoint = options.Endpoint is not null;
            var hasConnectionString = !string.IsNullOrWhiteSpace(options.ConnectionString);
            if (hasEndpoint == hasConnectionString)
            {
                throw new ArgumentException(
                    $"Set exactly one of {nameof(AzureAppConfigurationFeatureFlagsOptions.Endpoint)} or {nameof(AzureAppConfigurationFeatureFlagsOptions.ConnectionString)} to connect to Azure App Configuration.",
                    nameof(options));
            }

            if (options.RefreshInterval < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException(
                    $"{nameof(AzureAppConfigurationFeatureFlagsOptions.RefreshInterval)} must be at least 1 second.",
                    nameof(options));
            }
        }

        private static void ConfigureProvider(AzureAppConfigurationOptions provider, AzureAppConfigurationFeatureFlagsOptions options)
        {
            // Cheap to create: a DefaultAzureCredential only acquires a token when first used.
            var credential = options.Credential ?? CreateDefaultCredential();

            _ = options.Endpoint is not null
                ? provider.Connect(options.Endpoint, credential)
                : provider.Connect(options.ConnectionString);

            if (!options.IncludeKeyValues)
            {
                _ = provider.Select(NoKeyValuesKeyFilter);
            }

            _ = provider.UseFeatureFlags(featureFlags =>
            {
                _ = featureFlags.Select(KeyFilter.Any, LabelFilter.Null);
                if (!string.IsNullOrWhiteSpace(options.Label))
                {
                    // Selected after the unlabelled flags, so it overrides them.
                    _ = featureFlags.Select(KeyFilter.Any, options.Label);
                }

                _ = featureFlags.SetRefreshInterval(options.RefreshInterval);
            });

            // Only used if a loaded key-value is a Key Vault reference.
            _ = provider.ConfigureKeyVault(keyVault => keyVault.SetCredential(credential));

            options.ConfigureProvider?.Invoke(provider);
        }

        /// <summary>
        /// Same credential selection as Polochon.Messaging.AzureQueue's <c>DefaultAzureCredentialFactory</c>:
        /// <c>AZURE_TOKEN_CREDENTIALS</c> is deployment configuration, honored when set but never guessed.
        /// </summary>
        private static TokenCredential CreateDefaultCredential()
            => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DefaultAzureCredential.DefaultEnvironmentVariableName))
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(DefaultAzureCredential.DefaultEnvironmentVariableName, new DefaultAzureCredentialOptions());
    }
}
