using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.Modules;
using Polochon.Modules;

namespace Polochon.Messaging
{
    /// <summary>
    /// Provides extension methods for activating per-module inbox processing.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Activates this module's inbox processing: registers a per-module <see cref="InboxProcessor"/>
        /// that, on a fixed clock, reads every integration event buffered in this module's inbox and
        /// republishes it through the module's own notification pipeline.
        /// </summary>
        /// <remarks>
        /// Opt-in, and per module, deliberately: unlike <see cref="EventBus"/> (registered
        /// unconditionally by <c>AddPolochon()</c>), a process should only ever consume the inbox of
        /// the module(s) it actually hosts - see the remarks on <see cref="InboxProcessor"/>. Chain
        /// after <c>services.AddModule&lt;TModule&gt;()</c>, e.g.
        /// <c>services.AddModule&lt;TModule&gt;().WithInboxProcessing()</c>.
        /// </remarks>
        /// <typeparam name="TModule">The concrete module type the builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="clock">How often to read the module's inbox. Defaults to <see cref="InboxProcessor.DefaultClock"/>.</param>
        public static IModularModuleBuilder<TModule> WithInboxProcessing<TModule>(
            this IModularModuleBuilder<TModule> builder,
            TimeSpan? clock = null)
            where TModule : ModuleBase
        {
            var interval = clock ?? InboxProcessor.DefaultClock;

            return builder.ConfigureHostServices(services =>
                services.AddHostedService(sp => new InboxProcessor(
                    sp.GetServices<IModularModule>().OfType<TModule>().Single(),
                    interval,
                    sp.GetRequiredService<ILogger<InboxProcessor>>())));
        }
    }
}
