using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Modules;

namespace Polochon.Messaging
{
    /// <summary>
    /// Cross-module relay: on a fixed clock, drains each module's outbox and delivers every
    /// message to the inbox of every other module that has a registered handler for it.
    /// </summary>
    /// <remarks>
    /// Relies entirely on the in-memory, non-durable <see cref="IOutbox"/>/<see cref="IInbox"/>
    /// pair - see their remarks. A real cross-module bus with durable delivery is planned to
    /// replace this.
    /// </remarks>
    public class EventBus : BackgroundService
    {
        private const int BusClockInSeconds = 1;

        private readonly IEnumerable<IModularModule> modules;

        private readonly ILogger<EventBus> logger;

        private readonly SemaphoreSlim processingLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        /// <param name="modules">Every registered module, used both as message sources and destinations.</param>
        /// <param name="logger">Logger, resolved through the standard Microsoft.Extensions.Logging pipeline (see <c>AddPolochonLogging</c>/<c>WithSerilog</c>).</param>
        public EventBus(IEnumerable<IModularModule> modules, ILogger<EventBus> logger)
        {
            this.modules = modules;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Event bus start");
            return base.StartAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Event bus stopping - waiting for current iteration to complete");

            // Wait to acquire the lock, which ensures the current iteration is complete
            await processingLock.WaitAsync(cancellationToken);
            _ = processingLock.Release();

            logger.LogInformation("Event bus stop");
            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Relays, right now, every integration event currently waiting in a module's outbox to the
        /// inbox of every other module with a handler for it - the same pass the bus runs on its
        /// own clock. Waits for a pass already in progress to finish first.
        /// </summary>
        /// <remarks>
        /// For a process about to exit, e.g. after <c>ModuleBase.ProcessInboxAsync</c> in a job:
        /// outboxes are in memory, so whatever the handled messages published and the clock has not
        /// relayed yet would otherwise be lost with the process. Delivery errors are logged, not
        /// thrown, as on the clock.
        /// </remarks>
        /// <param name="cancellationToken">Cancels waiting for a pass in progress, and stops reading outboxes; a message being delivered is still delivered.</param>
        /// <returns>A task that completes once the pass is done.</returns>
        public async Task RelayPendingMessagesAsync(CancellationToken cancellationToken = default)
        {
            await processingLock.WaitAsync(cancellationToken);
            try
            {
                logger.LogDebug("Looking for new messages at {Time}", DateTime.UtcNow);

                // Don't pass the cancellation token to the message processing to allow graceful completion of in-flight messages
                var token = CancellationToken.None;

                foreach (var module in modules)
                {
                    try
                    {
                        await foreach (var item in module.Outbox.DrainPendingMessagesAsync(cancellationToken))
                        {
                            logger.LogDebug("Found message {MessageType} in module {Type}", item.GetType(), module.GetType());

                            // Only relay to modules that have a registered handler for this
                            // event, mirroring how commands/queries are routed to the one
                            // module that owns them (CanHandleCommand/CanHandleQuery) -
                            // except an integration event is pub/sub, so more than one
                            // module can match.
                            foreach (var destinationModule in modules.Where(x => x != module && x.CanHandleIntegrationEvent(item)))
                            {
                                try
                                {
                                    await destinationModule.Inbox.PublishMessageAsync(item, token);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Error sending message to module {Type}", destinationModule.GetType());
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fire and forget message delivery.
                        logger.LogError(ex, "Error draining outbox for module {Type}", module.GetType());
                    }
                }
            }
            finally
            {
                _ = processingLock.Release();
            }
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(BusClockInSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        logger.LogInformation("Event bus processing cancel requesting ; breaking loop");
                        break;
                    }

                    await RelayPendingMessagesAsync(stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("Internal task cancelled");
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                logger.LogInformation("Event bus iteration cancelled");
            }
        }
    }
}
