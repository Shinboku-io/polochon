using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Modules;

namespace Polochon.Messaging
{
    /// <summary>
    /// Per-module consumer of that module's own inbox: reads every integration event buffered
    /// there and republishes it through the module's own notification pipeline - which is where a
    /// registered <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/>, or any other
    /// <see cref="INotificationHandler{TNotification}"/>, actually reacts to it. Reads either on a
    /// fixed clock or only when this process delivers a message - see <see cref="InboxProcessingMode"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="EventBus"/> - which must run unconditionally for every module hosted in
    /// this process, since it is the only thing draining a module's outbox - this is opt-in, one
    /// instance per module (see <c>WithInboxProcessing</c> on <c>IModularModuleBuilder</c>).
    /// A distributed deployment only ever hosts (and so should only ever consume the inbox of) the
    /// module(s) it actually runs; a process with no local consumer for a module simply never
    /// activates one.
    /// </remarks>
    public sealed class InboxProcessor : BackgroundService
    {
        /// <summary>The read frequency used when <c>WithInboxProcessing</c> is not given an explicit clock.</summary>
        public static readonly TimeSpan DefaultClock = TimeSpan.FromSeconds(1);

        private readonly ModuleBase module;
        private readonly InboxProcessingOptions options;
        private readonly ILogger<InboxProcessor> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="InboxProcessor"/> class, reading the
        /// module's inbox on a fixed clock (<see cref="InboxProcessingMode.Polling"/>).
        /// </summary>
        /// <param name="module">The module whose own inbox this instance reads. Not <see cref="Abstractions.Modules.IModularModule"/> - it needs the concrete <see cref="ModuleBase"/> to reach the module's own internal container, where <see cref="IInboxReader"/> and <see cref="INotificationPublisher"/> live.</param>
        /// <param name="clock">How often to read the module's inbox.</param>
        /// <param name="logger">Logger, resolved through the standard Microsoft.Extensions.Logging pipeline.</param>
        public InboxProcessor(ModuleBase module, TimeSpan clock, ILogger<InboxProcessor> logger)
            : this(module, new InboxProcessingOptions { Mode = InboxProcessingMode.Polling, Clock = clock }, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InboxProcessor"/> class.
        /// </summary>
        /// <param name="module">The module whose own inbox this instance reads - see the other constructor.</param>
        /// <param name="options">When to read the module's inbox.</param>
        /// <param name="logger">Logger, resolved through the standard Microsoft.Extensions.Logging pipeline.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> is invalid - see <see cref="InboxProcessingOptions"/>.</exception>
        public InboxProcessor(ModuleBase module, InboxProcessingOptions options, ILogger<InboxProcessor> logger)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            this.module = module;
            this.options = options;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Inbox processor starting for module {Module} in {Mode} mode", module.Name, options.Mode);
            return base.StartAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Inbox processor stopping for module {Module}", module.Name);
            return base.StopAsync(cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (options.Mode == InboxProcessingMode.InProcess)
                {
                    var signal = module.GetRequiredService<InboxSignal>();

                    // Once up front: nothing will signal what a previous run left in a persistent inbox.
                    _ = await InboxDrainer.DrainAsync(module, logger, stoppingToken);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await WaitForDeliveryAsync(signal, stoppingToken);
                        _ = await InboxDrainer.DrainAsync(module, logger, stoppingToken);
                    }
                }
                else
                {
                    using var timer = new PeriodicTimer(options.Clock);
                    while (await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        _ = await InboxDrainer.DrainAsync(module, logger, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }
        }

        /// <summary>
        /// Waits for the next delivery into the inbox or, when <see cref="InboxProcessingOptions.IdleSweep"/>
        /// is set, for that long without one - whichever comes first.
        /// </summary>
        private async Task WaitForDeliveryAsync(InboxSignal signal, CancellationToken stoppingToken)
        {
            if (options.IdleSweep is not { } idleSweep)
            {
                await signal.WaitAsync(stoppingToken);
                return;
            }

            using var idle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            idle.CancelAfter(idleSweep);
            try
            {
                await signal.WaitAsync(idle.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug("Inbox of module {Module} idle for {IdleSweep}, reading it anyway", module.Name, idleSweep);
            }
        }
    }
}
