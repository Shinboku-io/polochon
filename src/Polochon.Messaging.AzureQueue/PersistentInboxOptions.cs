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
    }
}