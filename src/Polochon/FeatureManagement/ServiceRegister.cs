using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using Polochon.Abstractions.Modules;

namespace Polochon.FeatureManagement
{
    /// <summary>
    /// Lets a module evaluate feature flags whose definitions are owned by the host. Module-level only:
    /// the host sets up feature management itself with Microsoft.FeatureManagement's own
    /// <c>AddFeatureManagement()</c> - and, when needed, its definition source (e.g. Azure App
    /// Configuration, with its endpoint, identity and Key Vault references) - so none of that plumbing
    /// ever reaches a module's configuration.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Adds Microsoft.FeatureManagement to this module's isolated container (<see cref="IFeatureManager"/>,
        /// <see cref="IVariantFeatureManager"/>, built-in filters...), with its <see cref="IFeatureDefinitionProvider"/>
        /// forwarding to the host's. Requires the host to register feature management too, e.g.
        /// <c>services.AddFeatureManagement()</c>; module initialization fails otherwise.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        public static IModularModuleBuilder<TModule> WithFeatureManagement<TModule>(this IModularModuleBuilder<TModule> builder)
            where TModule : IModularModule
            => builder.WithFeatureManagement(static _ => { });

        /// <summary>
        /// Adds Microsoft.FeatureManagement to this module's isolated container (<see cref="IFeatureManager"/>,
        /// <see cref="IVariantFeatureManager"/>, built-in filters...), with its <see cref="IFeatureDefinitionProvider"/>
        /// forwarding to the host's. <paramref name="configureFeatureManagement"/> customizes evaluation
        /// within this module only, e.g. adding a custom feature filter or targeting. Requires the host to
        /// register feature management too, e.g. <c>services.AddFeatureManagement()</c>; module
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
                        "Register feature management on the host (e.g. services.AddFeatureManagement()).");

                configureFeatureManagement(services.AddFeatureManagement());

                // AddFeatureManagement() registers a provider reading IConfiguration, which a module
                // container does not have (on purpose): replace it with the host forwarder.
                services.RemoveAll<IFeatureDefinitionProvider>();
                services.AddSingleton<IFeatureDefinitionProvider>(new HostFeatureDefinitionProvider(hostProvider));
            });
        }
    }
}
