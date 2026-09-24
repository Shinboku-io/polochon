using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Modules;

namespace Polochon.Modules
{
    /// <summary>
    /// Provides extension methods for registering Polochon modules.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers <typeparamref name="TModule"/> as the <see cref="IModularModule"/> singleton and
        /// returns a strongly-typed builder that Polochon extension packages (e.g. Polochon.Serilog) or
        /// callers can use to layer additional configuration onto the module's isolated container, e.g.
        /// <c>services.AddModule&lt;TModule&gt;().WithSerilog()</c> or
        /// <c>services.AddModule&lt;TModule&gt;().ConfigureModule((services, module) => ...)</c>.
        /// </summary>
        /// <typeparam name="TModule">The concrete module type to register.</typeparam>
        /// <param name="services">The service collection to register with.</param>
        public static IModularModuleBuilder<TModule> AddModule<TModule>(this IServiceCollection services)
            where TModule : ModuleBase
        {
            var configurators = new List<Action<IServiceCollection, ModuleBase, IServiceProvider>>();

            _ = services.AddSingleton<IModularModule>(sp =>
            {
                var module = ActivatorUtilities.CreateInstance<TModule>(sp);
                foreach (var configure in configurators)
                {
                    // sp is the host's root provider (singleton factory): bound here so
                    // configurators needing a host service can reach it at module initialization.
                    module.AddConfigurator((moduleServices, configuredModule) => configure(moduleServices, configuredModule, sp));
                }

                return module;
            });

            return new ModularModuleBuilder<TModule>(configurators, services);
        }
    }
}
