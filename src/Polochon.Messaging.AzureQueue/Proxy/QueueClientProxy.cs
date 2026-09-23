using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Proxy class for interacting with Azure Queue storage with telemetry support.
    /// </summary>
    public class QueueClientProxy : IQueueClientProxy
    {
        private readonly QueueClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClientProxy"/> class.
        /// </summary>
        /// <param name="queueClient">The Azure Queue client.</param>
        public QueueClientProxy(QueueClient queueClient)
        {
            client = queueClient;
        }

        /// <summary>
        /// Creates the queue if it does not already exist.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<Response> CreateIfNotExistsAsync()
        {
            return client.CreateIfNotExistsAsync();
        }

        /// <summary>
        /// Deletes the queue if it exists.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, with a boolean result indicating whether the queue was deleted.</returns>
        public Task<Response<bool>> DeleteIfExistsAsync()
        {
            return client.DeleteIfExistsAsync();
        }

        /// <summary>
        /// Checks if the queue exists.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, with a boolean result indicating whether the queue exists.</returns>
        public Task<Response<bool>> ExistsAsync()
        {
            return client.ExistsAsync();
        }

        /// <summary>
        /// Peeks messages from the queue without removing them.
        /// </summary>
        /// <param name="maxMessages">The maximum number of messages to peek.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation, with an array of peeked messages.</returns>
        public Task<Response<PeekedMessage[]>> PeekMessagesAsync(
            int? maxMessages = 32,
            CancellationToken cancellationToken = default)
        {
            return client.PeekMessagesAsync(maxMessages, cancellationToken);
        }

        /// <summary>
        /// Sends a message to the queue.
        /// </summary>
        /// <param name="messageText">The message text to send.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation, with a send receipt.</returns>
        public Task<Response<SendReceipt>> SendMessageAsync(
            string messageText,
            CancellationToken cancellationToken = default)
        {
            return client.SendMessageAsync(messageText, cancellationToken);
        }

        /// <summary>
        /// Receives messages from the queue.
        /// </summary>
        /// <param name="maxMessages">The maximum number of messages to receive.</param>
        /// <param name="visibilityTimeout">The visibility timeout for the messages.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation, with an array of received messages.</returns>
        public Task<Response<QueueMessage[]>> ReceiveMessagesAsync(
            int maxMessages = 32,
            TimeSpan? visibilityTimeout = default,
            CancellationToken cancellationToken = default)
        {
            return client.ReceiveMessagesAsync(maxMessages, visibilityTimeout, cancellationToken);
        }

        /// <summary>
        /// Deletes a message from the queue.
        /// </summary>
        /// <param name="messageId">The ID of the message to delete.</param>
        /// <param name="popReceipt">The pop receipt of the message to delete.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<Response> DeleteMessageAsync(
            string messageId,
            string popReceipt,
            CancellationToken cancellationToken = default)
        {
            return client.DeleteMessageAsync(messageId, popReceipt, cancellationToken);
        }

        /// <summary>
        /// Clears all messages from the queue.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<Response> ClearMessagesAsync(
           CancellationToken cancellationToken = default)
        {
            return client.ClearMessagesAsync(cancellationToken);
        }
    }
}