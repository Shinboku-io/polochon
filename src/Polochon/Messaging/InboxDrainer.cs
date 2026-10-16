using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Security;
using Polochon.Modules;
using Polochon.Security;

namespace Polochon.Messaging
{
    /// <summary>
    /// Reads a module's inbox until it is empty and republishes every integration event through the
    /// module's own notification pipeline. Shared by the <see cref="InboxProcessor"/> background
    /// loop and the on-demand <see cref="ModuleBase.ProcessInboxAsync"/>, so both handle a message
    /// exactly the same way.
    /// </summary>
    internal static class InboxDrainer
    {
        public static async Task<InboxDrainResult> DrainAsync(ModuleBase module, ILogger logger, CancellationToken cancellationToken)
        {
            var inboxReader = module.GetRequiredService<IInboxReader>();
            var errorQueue = module.GetRequiredService<IErrorQueue>();

            // Events are processed in the drain's own context, not on behalf of the user whose
            // command raised them: that command was already authorized when it mutated state.
            var systemPrincipal = SystemIdentity.CreatePrincipal($"{module.Name} inbox processor");

            var processed = 0;
            var failed = 0;

            await foreach (var message in inboxReader.ReadAllMessagesAsync(cancellationToken))
            {
                processed++;
                logger.LogDebug("Processing inbox message {MessageType} for module {Module}", message.GetType(), module.Name);

                // A fresh DI scope per message: handlers (and whatever they depend on
                // transitively, e.g. a unit of work) are meant to live for one unit of
                // work, not for the lifetime of the drain.
                using var scope = module.ServiceProvider.CreateScope();
                using var identity = AmbientUserContext.Use(systemPrincipal);
                var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

                try
                {
                    // Not the caller's token: let an in-flight message finish even while shutting down.
                    await publisher.PublishAsync(message, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    failed++;
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

            return new InboxDrainResult(processed, failed);
        }
    }
}
