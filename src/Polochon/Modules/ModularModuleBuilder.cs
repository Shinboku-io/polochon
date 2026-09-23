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
        private readonly List<Action<IServiceCollection, ModuleBase>> _configurators;
        private readonly IServiceCollection _hostServices;

        public ModularModuleBuilder(List<Action<IServiceCollection, ModuleBase>> configurators, IServiceCollection hostServices)
        {
            _configurators = configurators;
            _hostServices = hostServices;
        }

        public IModularModuleBuilder<TModule> ConfigureModule(Action<IServiceCollection, TModule> configure)
        {
            _configurators.Add((services, module) => configure(services, (TModule)module));
            return this;
        }

        public IModularModuleBuilder<TModule> ConfigureHostServices(Action<IServiceCollection> configure)
        {
            configure(_hostServices);
            return this;
        }
    }
}
