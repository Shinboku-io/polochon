using Polochon.Abstractions.Messaging;

namespace Polochon.Messaging.AzureQueue
{
    /// <summary>
    /// Connects an <see cref="AzureQueueInbox"/> to a specific Azure Storage Queue account and pair
    /// of queues (inbox + error).
    /// </summary>
    public class PersistentInboxOptions
    {
        /// <summary>
        /// The queue service endpoint, e.g. <c>https://{account}.queue.core.windows.net</c>, or
        /// <c>UseDevelopmentStorage=true</c> for the local storage emulator.
        /// </summary>
        public required string QueueEndpoint { get; set; }

        /// <summary>The name of this module's inbox queue.</summary>
        public required string InboxQueueName { get; set; }

        /// <summary>
        /// The name of this module's error queue - where a message ends up if it could not be
        /// read (see <see cref="AzureQueueInbox.ReadAllMessagesAsync"/>) or, once read, failed
        /// processing (see <see cref="IErrorQueue"/>).
        /// </summary>
        public string ErrorQueueName { get; set; } = "error-queue";

        /// <summary>
        /// How many times a message may be received from the inbox queue before it is rejected as
        /// poison: once its dequeue count exceeds this, it is moved to the error queue without
        /// being processed again. Defaults to 5; must be at least 1.
        /// </summary>
        /// <remarks>
        /// A message is only received again when an earlier attempt did not complete - the process
        /// stopped or crashed while handling it, or handling took longer than the visibility timeout.
        /// Without this limit, a message that crashes the process every time would be retried
        /// forever. A handler that throws does not count: that failure already routes the message to
        /// the error queue on the first attempt. Messages received in the same batch as a crashing
        /// one are received again too, so keep some headroom above 1.
        /// </remarks>
        public int MaxDequeueCount { get; set; } = 5;
    }
}