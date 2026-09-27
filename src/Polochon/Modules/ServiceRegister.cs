using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        /// <summary>
        /// Hands a module its own settings object, provided by the host at registration time (e.g. bound
        /// from a configuration section), by registering it into the module's isolated container - where the
        /// host's own <c>IOptions&lt;T&gt;</c> registrations never reach. Handlers and services inside the
        /// module can then inject either <see cref="IOptions{TOptions}"/> or <typeparamref name="TOptions"/>
        /// itself, e.g. <c>services.AddModule&lt;TModule&gt;().WithOptions(new MyModuleOptions { ... })</c>.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <typeparam name="TOptions">The module's settings type.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="options">The settings instance, shared as a singleton within the module.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IModularModuleBuilder<TModule> WithOptions<TModule, TOptions>(this IModularModuleBuilder<TModule> builder, TOptions options)
            where TModule : IModularModule
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            return builder.ConfigureModule((services, _, _) =>
            {
                _ = services.AddSingleton(options);
                _ = services.AddSingleton(Options.Create(options));
            });
        }
    }
}
