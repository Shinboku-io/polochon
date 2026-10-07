using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Modules;

namespace Polochon.Modules
{
    /// <summary>
    /// Default <see cref="IModularModuleBuilder{TModule}"/> implementation returned by <c>AddModule&lt;TModule&gt;()</c>.
    /// </summary>
    /// <typeparam name="TModule">The concrete module type this builder was created for.</typeparam>
    internal sealed class ModularModuleBuilder<TModule> : IModularModuleBuilder<TModule>
        where TModule : ModuleBase
    {
        private readonly List<Action<IServiceCollection, ModuleBase, IServiceProvider>> configurators;
        private readonly IServiceCollection hostServices;

        public ModularModuleBuilder(List<Action<IServiceCollection, ModuleBase, IServiceProvider>> configurators, IServiceCollection hostServices)
        {
            this.configurators = configurators;
            this.hostServices = hostServices;
        }

        public IModularModuleBuilder<TModule> ConfigureModule(Action<IServiceCollection, TModule, IServiceProvider> configure)
        {
            configurators.Add((services, module, hostServices) => configure(services, (TModule)module, hostServices));
            return this;
        }

        public IModularModuleBuilder<TModule> ConfigureHostServices(Action<IServiceCollection> configure)
        {
            configure(hostServices);
            return this;
        }
    }
}
