using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polochon.Abstractions.Modules;
using Polochon.Abstractions.Security;
using Polochon.Modules;

namespace Polochon.Security
{
    /// <summary>
    /// Provides extension methods for registering the user identity used by message validators.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers <see cref="ClaimsUserIdentityProvider"/> as the singleton
        /// <see cref="IUserIdentityProvider"/>, unless one is already registered. Called
        /// automatically by <c>AddPolochon()</c> and by every module's base configuration.
        /// </summary>
        /// <remarks>
        /// To override it in the host, register your own <see cref="IUserIdentityProvider"/>
        /// before or after <c>AddPolochon()</c>. To override it in a module, use
        /// <see cref="WithUserIdentity{TModule}"/>.
        /// </remarks>
        /// <param name="services">The service collection to register with.</param>
        public static IServiceCollection AddPolochonUserIdentity(this IServiceCollection services)
        {
            services.TryAddSingleton<IUserIdentityProvider, ClaimsUserIdentityProvider>();

            return services;
        }

        /// <summary>
        /// Replaces the <see cref="IUserIdentityProvider"/> in this module's isolated container,
        /// e.g. <c>services.AddModule&lt;TModule&gt;().WithUserIdentity(_ =&gt; new MyProvider())</c>.
        /// </summary>
        /// <typeparam name="TModule">The concrete module type the builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="factory">Creates the provider from the module's own service provider. It is registered as a singleton.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
        public static IModularModuleBuilder<TModule> WithUserIdentity<TModule>(
            this IModularModuleBuilder<TModule> builder,
            Func<IServiceProvider, IUserIdentityProvider> factory)
            where TModule : ModuleBase
        {
            ArgumentNullException.ThrowIfNull(factory);

            // Replace, not TryAdd: module configurators run after the base configuration has
            // already registered the default provider.
            return builder.ConfigureModule((services, _) =>
                services.Replace(ServiceDescriptor.Singleton(factory)));
        }
    }
}
