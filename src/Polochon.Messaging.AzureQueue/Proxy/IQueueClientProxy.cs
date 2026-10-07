using Azure;
using Azure.Storage.Queues.Models;

namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Defines a proxy interface for interacting with Azure Queue storage.
    /// </summary>
    public interface IQueueClientProxy
    {
        /// <summary>
        /// Creates the queue if it does not already exist.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the service.</returns>
        Task<Response> CreateIfNotExistsAsync();

        /// <summary>
        /// Deletes the queue if it exists.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the queue was deleted.</returns>
        Task<Response<bool>> DeleteIfExistsAsync();

        /// <summary>
        /// Checks if the queue exists.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the queue exists.</returns>
        Task<Response<bool>> ExistsAsync();

        /// <summary>
        /// Sends a message to the queue.
        /// </summary>
        /// <param name="messageText">The message text to send.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the send receipt.</returns>
        Task<Response<SendReceipt>> SendMessageAsync(
            string messageText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Peeks messages from the queue without removing them.
        /// </summary>
        /// <param name="maxMessages">The maximum number of messages to peek.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of peeked messages.</returns>
        Task<Response<PeekedMessage[]>> PeekMessagesAsync(
            int? maxMessages = 32,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Receives messages from the queue.
        /// </summary>
        /// <param name="maxMessages">The maximum number of messages to receive.</param>
        /// <param name="visibilityTimeout">The visibility timeout for the messages.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of received messages.</returns>
        Task<Response<QueueMessage[]>> ReceiveMessagesAsync(
            int maxMessages = 32,
            TimeSpan? visibilityTimeout = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a message from the queue.
        /// </summary>
        /// <param name="messageId">The ID of the message to delete.</param>
        /// <param name="popReceipt">The pop receipt of the message to delete.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the service.</returns>
        Task<Response> DeleteMessageAsync(
            string messageId,
            string popReceipt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all messages from the queue.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the service.</returns>
        Task<Response> ClearMessagesAsync(
             CancellationToken cancellationToken = default);
    }
}