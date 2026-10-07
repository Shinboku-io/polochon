using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Security;
using Polochon.Modules;
using Polochon.Security;

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
            var inboxReader = module.GetRequiredService<IInboxReader>();
            var errorQueue = module.GetRequiredService<IErrorQueue>();
            var timer = new PeriodicTimer(clock);

            // Events are processed in the processor's own context, not on behalf of the user
            // whose command raised them: that command was already authorized when it mutated state.
            var systemPrincipal = SystemIdentity.CreatePrincipal($"{module.Name} inbox processor");

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await foreach (var message in inboxReader.ReadAllMessagesAsync(stoppingToken))
                    {
                        logger.LogDebug("Processing inbox message {MessageType} for module {Module}", message.GetType(), module.Name);

                        // A fresh DI scope per message: handlers (and whatever they depend on
                        // transitively, e.g. a unit of work) are meant to live for one unit of
                        // work, not for the lifetime of this long-running background service.
                        using var scope = module.ServiceProvider.CreateScope();
                        using var identity = AmbientUserContext.Use(systemPrincipal);
                        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

                        try
                        {
                            // Not the stopping token: let an in-flight message finish even while shutting down.
                            await publisher.PublishAsync(message, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing inbox message {MessageType} for module {Module}", message.GetType(), module.Name);

                            try
                            {
                                await errorQueue.PublishFailedMessageAsync(message, ex, CancellationToken.None);
                            }
                            catch (Exception errorQueueEx)
                            {
                                // The error queue is the last line of defence - if it also fails,
                                // there is nowhere left to route the message but the log, so it
                                // does not silently vanish.
                                logger.LogError(errorQueueEx, "Error queue itself failed for message {MessageType} for module {Module}", message.GetType(), module.Name);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }
        }
    }
}
