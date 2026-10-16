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

        /// <summary>
        /// Activates this module's inbox processing as configured by <paramref name="options"/>:
        /// registers a per-module <see cref="InboxProcessor"/> that reads every integration event
        /// buffered in this module's inbox - on a fixed clock, or only when this process delivers
        /// one (see <see cref="InboxProcessingMode"/>) - and republishes it through the module's own
        /// notification pipeline.
        /// </summary>
        /// <remarks>
        /// Use <see cref="InboxProcessingMode.InProcess"/> when every module publishing to this one
        /// runs in the same process (a modular monolith): a persistent inbox (e.g. an Azure Storage
        /// Queue) is then only read when a message is actually delivered, instead of every
        /// <see cref="InboxProcessingOptions.Clock"/>. Keep <see cref="InboxProcessingMode.Polling"/>
        /// when other processes can deliver into this module's inbox. Opt-in and per module for the
        /// same reasons as <see cref="WithInboxProcessing{TModule}(IModularModuleBuilder{TModule}, TimeSpan?)"/>.
        /// </remarks>
        /// <typeparam name="TModule">The concrete module type the builder was created for.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="options">When to read the module's inbox.</param>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> is invalid - see <see cref="InboxProcessingOptions"/>.</exception>
        public static IModularModuleBuilder<TModule> WithInboxProcessing<TModule>(
            this IModularModuleBuilder<TModule> builder,
            InboxProcessingOptions options)
            where TModule : ModuleBase
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            // Fail at registration, not when the host starts the processor.
            options.Validate();

            return builder.ConfigureHostServices(services =>
                services.AddHostedService(sp => new InboxProcessor(
                    sp.GetServices<IModularModule>().OfType<TModule>().Single(),
                    options,
                    sp.GetRequiredService<ILogger<InboxProcessor>>())));
        }
    }
}
