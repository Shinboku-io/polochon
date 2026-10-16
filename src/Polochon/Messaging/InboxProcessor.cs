using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Modules;

namespace Polochon.Messaging
{
    /// <summary>
    /// Per-module consumer of that module's own inbox: on a fixed clock, reads every integration
    /// event buffered there and republishes it through the module's own notification pipeline -
    /// which is where a registered <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/>,
    /// or any other <see cref="INotificationHandler{TNotification}"/>, actually reacts to it.
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
        private readonly TimeSpan clock;
        private readonly ILogger<InboxProcessor> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="InboxProcessor"/> class.
        /// </summary>
        /// <param name="module">The module whose own inbox this instance reads. Not <see cref="Abstractions.Modules.IModularModule"/> - it needs the concrete <see cref="ModuleBase"/> to reach the module's own internal container, where <see cref="IInboxReader"/> and <see cref="INotificationPublisher"/> live.</param>
        /// <param name="clock">How often to read the module's inbox.</param>
        /// <param name="logger">Logger, resolved through the standard Microsoft.Extensions.Logging pipeline.</param>
        public InboxProcessor(ModuleBase module, TimeSpan clock, ILogger<InboxProcessor> logger)
        {
            this.module = module;
            this.clock = clock;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Inbox processor starting for module {Module}", module.Name);
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
            using var timer = new PeriodicTimer(clock);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    _ = await InboxDrainer.DrainAsync(module, logger, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }
        }
    }
}
