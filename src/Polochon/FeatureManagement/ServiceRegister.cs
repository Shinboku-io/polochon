using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using Polochon.Abstractions.Modules;

namespace Polochon.FeatureManagement
{
    /// <summary>
    /// Feature flags for Polochon hosts and modules, based on Microsoft.FeatureManagement. The host owns
    /// the feature definitions and where they come from (configuration, or e.g. Azure App Configuration
    /// with its endpoint, identity and Key Vault references); a module only evaluates them - none of that
    /// plumbing ever reaches a module's configuration.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers feature management on the host, reading feature definitions from the host's
        /// configuration (the <c>FeatureManagement</c> section). Chain this after <c>AddPolochon()</c>,
        /// e.g. <c>services.AddPolochon().WithFeatureManagement()</c>, so modules registered with
        /// <c>WithFeatureManagement()</c> can evaluate those definitions.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection WithFeatureManagement(this IServiceCollection services)
            => services.WithFeatureManagement(static _ => { });

        /// <summary>
        /// Registers feature management on the host, reading feature definitions from the host's
        /// configuration (the <c>FeatureManagement</c> section). <paramref name="configureFeatureManagement"/>
        /// customizes evaluation on the host itself, e.g. adding a feature filter host code uses - modules
        /// register their own filters through their own <c>WithFeatureManagement()</c>.
        /// </summary>
        /// <remarks>
        /// Safe to combine with anything that registered feature management on the host already (e.g.
        /// <c>AddAzureAppConfigurationFeatureFlags()</c> or <c>AddScopedFeatureManagement()</c>, in either
        /// order): that registration is kept - with its lifetime - and only
        /// <paramref name="configureFeatureManagement"/> is applied to it, instead of registering
        /// Microsoft.FeatureManagement a second time.
        /// </remarks>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configureFeatureManagement">A callback used to customize the host's feature management.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection WithFeatureManagement(this IServiceCollection services, Action<IFeatureManagementBuilder> configureFeatureManagement)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureFeatureManagement);

            var featureManagement = services.Any(descriptor => descriptor.ServiceType == typeof(IFeatureManager))
                ? new ExistingFeatureManagementBuilder(services)
                : services.AddFeatureManagement();
            configureFeatureManagement(featureManagement);

            return services;
        }

        /// <summary>
        /// Adds Microsoft.FeatureManagement to this module's isolated container (<see cref="IFeatureManager"/>,
        /// <see cref="IVariantFeatureManager"/>, built-in filters...). Its <see cref="IFeatureDefinitionProvider"/>
        /// exposes the host's flags named <c>{module name}.{flag}</c> under their short name (the host's
        /// <c>inventory.BulkImport</c> is <c>BulkImport</c> in the <c>inventory</c> module); other flags are
        /// invisible to the module. Requires the host to register feature management too, e.g.
        /// <c>services.AddPolochon().WithFeatureManagement()</c>; module initialization fails otherwise.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        public static IModularModuleBuilder<TModule> WithFeatureManagement<TModule>(this IModularModuleBuilder<TModule> builder)
            where TModule : IModularModule
            => builder.WithFeatureManagement(static _ => { });

        /// <summary>
        /// Adds Microsoft.FeatureManagement to this module's isolated container (<see cref="IFeatureManager"/>,
        /// <see cref="IVariantFeatureManager"/>, built-in filters...). Its <see cref="IFeatureDefinitionProvider"/>
        /// exposes the host's flags named <c>{module name}.{flag}</c> under their short name; other flags are
        /// invisible to the module. <paramref name="configureFeatureManagement"/> customizes evaluation
        /// within this module only, e.g. adding a custom feature filter or targeting. Requires the host to
        /// register feature management too, e.g. <c>services.AddPolochon().WithFeatureManagement()</c>; module
        /// initialization fails otherwise.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="configureFeatureManagement">A callback used to customize the module's feature management.</param>
        public static IModularModuleBuilder<TModule> WithFeatureManagement<TModule>(
            this IModularModuleBuilder<TModule> builder,
            Action<IFeatureManagementBuilder> configureFeatureManagement)
            where TModule : IModularModule
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureFeatureManagement);

            return builder.ConfigureModule((services, module, hostServices) =>
            {
                // Resolved now, at module initialization, rather than on first flag evaluation: a host
                // missing feature management fails at startup instead of on some later request.
                var hostProvider = hostServices.GetService<IFeatureDefinitionProvider>()
                    ?? throw new InvalidOperationException(
                        $"Module '{module.Name}' uses feature management, but the host has no {nameof(IFeatureDefinitionProvider)} registered. " +
                        "Register feature management on the host (e.g. services.AddPolochon().WithFeatureManagement()).");

                configureFeatureManagement(services.AddFeatureManagement());

                // AddFeatureManagement() registers a provider reading IConfiguration, which a module
                // container does not have (on purpose): replace it with a view of the host's definitions
                // restricted to this module's flags ({module name}.{flag}).
                services.RemoveAll<IFeatureDefinitionProvider>();
                services.AddSingleton<IFeatureDefinitionProvider>(new HostFeatureDefinitionProvider(hostProvider, module.Name));
            });
        }
    }
}
