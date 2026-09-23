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

                    await processingLock.WaitAsync(stoppingToken);
                    try
                    {
                        logger.LogDebug("Looking for new messages at {Time}", DateTime.UtcNow);

                        // Don't pass the stopping token to the message processing to allow graceful completion of in-flight messages
                        var token = CancellationToken.None;

                        foreach (var module in modules)
                        {
                            try
                            {
                                await foreach (var item in module.Outbox.DrainPendingMessagesAsync(stoppingToken))
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
