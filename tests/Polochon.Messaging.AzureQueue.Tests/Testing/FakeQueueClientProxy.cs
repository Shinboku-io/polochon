using Azure;
using Azure.Storage.Queues.Models;
using Polochon.Messaging.AzureQueue.Proxy;

namespace Polochon.Messaging.AzureQueue.Tests.Testing
{
    /// <summary>
    /// In-memory <see cref="IQueueClientProxy"/> used in place of a real Azure Storage Queue.
    /// A message is removed as soon as it is received (not just made invisible), since every test
    /// using it reads a message once and immediately deletes it - the same pattern
    /// <see cref="AzureQueueInbox.ReadAllMessagesAsync"/> follows.
    /// </summary>
    internal sealed class FakeQueueClientProxy : IQueueClientProxy
    {
        private readonly Queue<string> messages = new();

        /// <summary>Every message ever sent to this queue, in send order - kept even after being received/deleted.</summary>
        public List<string> SentMessages { get; } = [];

        /// <summary>The message ids this proxy has been asked to delete.</summary>
        public List<string> DeletedMessageIds { get; } = [];

        public Task<Response> CreateIfNotExistsAsync() => Task.FromResult<Response>(new FakeAzureResponse());

        public Task<Response<bool>> DeleteIfExistsAsync() => Task.FromResult(Response.FromValue(true, new FakeAzureResponse()));

        public Task<Response<bool>> ExistsAsync() => Task.FromResult(Response.FromValue(true, new FakeAzureResponse()));

        public Task<Response<SendReceipt>> SendMessageAsync(string messageText, CancellationToken cancellationToken = default)
        {
            messages.Enqueue(messageText);
            SentMessages.Add(messageText);

            var receipt = QueuesModelFactory.SendReceipt(
                messageId: Guid.NewGuid().ToString(),
                insertionTime: DateTimeOffset.UtcNow,
                expirationTime: DateTimeOffset.UtcNow.AddDays(7),
                popReceipt: Guid.NewGuid().ToString(),
                timeNextVisible: DateTimeOffset.UtcNow);

            return Task.FromResult(Response.FromValue(receipt, new FakeAzureResponse()));
        }

        public Task<Response<PeekedMessage[]>> PeekMessagesAsync(int? maxMessages = 32, CancellationToken cancellationToken = default)
            => Task.FromResult(Response.FromValue(Array.Empty<PeekedMessage>(), (Response)new FakeAzureResponse()));

        public Task<Response<QueueMessage[]>> ReceiveMessagesAsync(int maxMessages = 32, TimeSpan? visibilityTimeout = default, CancellationToken cancellationToken = default)
        {
            var received = new List<QueueMessage>();
            while (received.Count < maxMessages && messages.Count > 0)
            {
                var body = messages.Dequeue();
                received.Add(QueuesModelFactory.QueueMessage(
                    messageId: Guid.NewGuid().ToString(),
                    popReceipt: Guid.NewGuid().ToString(),
                    messageText: body,
                    dequeueCount: 1));
            }

            return Task.FromResult(Response.FromValue(received.ToArray(), (Response)new FakeAzureResponse()));
        }

        public Task<Response> DeleteMessageAsync(string messageId, string popReceipt, CancellationToken cancellationToken = default)
        {
            DeletedMessageIds.Add(messageId);
            return Task.FromResult<Response>(new FakeAzureResponse());
        }

        public Task<Response> ClearMessagesAsync(CancellationToken cancellationToken = default)
        {
            messages.Clear();
            return Task.FromResult<Response>(new FakeAzureResponse());
        }
    }
}
