using Microsoft.Extensions.Hosting;
using Polochon.Abstractions.Modules;

namespace Polochon.Modules
{
    internal class LifecycleService : IHostedService
    {
        private readonly IEnumerable<IModularModule> modules;

        public LifecycleService(IEnumerable<IModularModule> modules)
        {
            this.modules = modules;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var module in modules)
            {
                await module.InitializeAsync();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var module in modules)
            {
                await module.DisposeAsync();
            }
        }
    }
}