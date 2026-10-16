using Microsoft.Extensions.Logging.Abstractions;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Messaging.AzureQueue.Tests.Testing;
using Xunit;

namespace Polochon.Messaging.AzureQueue.Tests
{
    /// <summary>
    /// Tests for <see cref="AzureQueueInbox"/>: the inbox side (publish/read round-trip) and the
    /// error-queue side, for both a message that fails processing after being read successfully
    /// (<see cref="IErrorQueue.PublishFailedMessageAsync"/>) and one that could not be read at all
    /// (an unrecognized or malformed message on the inbox queue).
    /// </summary>
    public sealed class AzureQueueInboxTests
    {
        private sealed record TestIntegrationEvent : IIntegrationEvent
        {
            public TestIntegrationEvent(string payload)
            {
                Payload = payload;
            }

            public string Payload { get; init; }

            public Guid Id { get; init; } = Guid.NewGuid();

            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        }

        private static (AzureQueueInbox Inbox, FakeQueueClientProxyFactory Factory, PersistentInboxOptions Options) CreateInbox()
        {
            var factory = new FakeQueueClientProxyFactory();
            var options = new PersistentInboxOptions
            {
                QueueEndpoint = "https://fake.queue.core.windows.net",
                InboxQueueName = "inbox",
                ErrorQueueName = "errors",
            };

            var inbox = new AzureQueueInbox(factory, options, NullLogger<AzureQueueInbox>.Instance);
            return (inbox, factory, options);
        }

        private static async Task<List<IIntegrationEvent>> ReadAllAsync(AzureQueueInbox inbox)
        {
            var result = new List<IIntegrationEvent>();
            await foreach (var message in inbox.ReadAllMessagesAsync(CancellationToken.None))
            {
                result.Add(message);
            }

            return result;
        }

        /// <summary>A published message round-trips back to an equal event, and is removed from the queue.</summary>
        [Fact]
        public async Task PublishThenRead_RoundTripsTheEvent_AndDeletesIt()
        {
            var (inbox, factory, options) = CreateInbox();
            var message = new TestIntegrationEvent("hello");

            await inbox.PublishMessageAsync(message, CancellationToken.None);
            var read = await ReadAllAsync(inbox);

            var readBack = Assert.IsType<TestIntegrationEvent>(Assert.Single(read));
            Assert.Equal("hello", readBack.Payload);
            Assert.Equal(message.Id, readBack.Id);

            var inboxProxy = factory.GetProxy(options.InboxQueueName);
            Assert.Single(inboxProxy.DeletedMessageIds);

            // The message is gone: reading again yields nothing.
            Assert.Empty(await ReadAllAsync(inbox));
        }

        /// <summary>
        /// A message that fails processing after being read successfully - the
        /// <see cref="IErrorQueue"/> path - is routed to the error queue with the original payload
        /// and the exception, and does not throw.
        /// </summary>
        [Fact]
        public async Task PublishFailedMessageAsync_SendsOriginalPayloadAndExceptionToErrorQueue()
        {
            var (inbox, factory, options) = CreateInbox();
            var message = new TestIntegrationEvent("boom");
            var exception = new InvalidOperationException("handler blew up");

            await inbox.PublishFailedMessageAsync(message, exception, CancellationToken.None);

            var errorProxy = factory.GetProxy(options.ErrorQueueName);
            var sent = Assert.Single(errorProxy.SentMessages);
            Assert.Contains("boom", sent, StringComparison.Ordinal);
            Assert.Contains("handler blew up", sent, StringComparison.Ordinal);
        }

        /// <summary>
        /// A message on the inbox queue that is not valid JSON does not crash
        /// <see cref="AzureQueueInbox.ReadAllMessagesAsync"/> - it is skipped, deleted from the
        /// inbox (so it is not retried forever), and routed to the error queue instead of being
        /// silently dropped.
        /// </summary>
        [Fact]
        public async Task ReadAllMessagesAsync_WithMalformedMessage_RoutesToErrorQueue_AndDoesNotThrow()
        {
            var (inbox, factory, options) = CreateInbox();
            var inboxProxy = factory.GetProxy(options.InboxQueueName);
            await inboxProxy.SendMessageAsync("this is not json", CancellationToken.None);

            var read = await ReadAllAsync(inbox);

            Assert.Empty(read);
            Assert.Single(inboxProxy.DeletedMessageIds);

            var errorProxy = factory.GetProxy(options.ErrorQueueName);
            Assert.Single(errorProxy.SentMessages);
        }

        /// <summary>
        /// A message on the inbox queue whose envelope is valid but names an unknown message type
        /// is treated the same way as a malformed one: routed to the error queue, not thrown.
        /// </summary>
        [Fact]
        public async Task ReadAllMessagesAsync_WithUnknownMessageType_RoutesToErrorQueue()
        {
            var (inbox, factory, options) = CreateInbox();
            var inboxProxy = factory.GetProxy(options.InboxQueueName);
            await inboxProxy.SendMessageAsync(
                """{"message-type":"Some.Unknown.Type, NoSuchAssembly","payload":"{}"}""",
                CancellationToken.None);

            var read = await ReadAllAsync(inbox);

            Assert.Empty(read);
            var errorProxy = factory.GetProxy(options.ErrorQueueName);
            Assert.Single(errorProxy.SentMessages);
        }

        /// <summary>Puts a valid message for <paramref name="payload"/> back on the inbox queue as if already received <paramref name="dequeueCount"/> - 1 times.</summary>
        private static async Task EnqueueRedeliveredAsync(AzureQueueInbox inbox, FakeQueueClientProxy inboxProxy, string payload, long dequeueCount)
        {
            await inbox.PublishMessageAsync(new TestIntegrationEvent(payload), CancellationToken.None);
            var body = inboxProxy.SentMessages[^1];
            _ = await inboxProxy.ClearMessagesAsync(CancellationToken.None);
            inboxProxy.EnqueueRedelivered(body, dequeueCount);
        }

        /// <summary>Poison messages are rejected after five receptions by default.</summary>
        [Fact(DisplayName = "Max dequeue count defaults to five")]
        public void MaxDequeueCountDefaultsToFive()
        {
            Assert.Equal(5, new PersistentInboxOptions { QueueEndpoint = "e", InboxQueueName = "q" }.MaxDequeueCount);
        }

        /// <summary>A message received more often than allowed is rejected as poison: not handed out, deleted, and sent to the error queue.</summary>
        [Fact(DisplayName = "A message received more than the max dequeue count is rejected as poison")]
        public async Task MessageOverMaxDequeueCountIsRejectedAsPoison()
        {
            var (inbox, factory, options) = CreateInbox();
            var inboxProxy = factory.GetProxy(options.InboxQueueName);
            await EnqueueRedeliveredAsync(inbox, inboxProxy, "crashes-every-time", options.MaxDequeueCount + 1);

            var read = await ReadAllAsync(inbox);

            Assert.Empty(read);
            Assert.Single(inboxProxy.DeletedMessageIds);
            var sent = Assert.Single(factory.GetProxy(options.ErrorQueueName).SentMessages);
            Assert.Contains("poison", sent, StringComparison.Ordinal);
            Assert.Contains("crashes-every-time", sent, StringComparison.Ordinal);
        }

        /// <summary>A message received exactly the allowed number of times still gets its last attempt.</summary>
        [Fact(DisplayName = "A message at the max dequeue count is still processed")]
        public async Task MessageAtMaxDequeueCountIsProcessed()
        {
            var (inbox, factory, options) = CreateInbox();
            var inboxProxy = factory.GetProxy(options.InboxQueueName);
            await EnqueueRedeliveredAsync(inbox, inboxProxy, "last-chance", options.MaxDequeueCount);

            var read = await ReadAllAsync(inbox);

            var readBack = Assert.IsType<TestIntegrationEvent>(Assert.Single(read));
            Assert.Equal("last-chance", readBack.Payload);
            Assert.Empty(factory.GetProxy(options.ErrorQueueName).SentMessages);
        }

        /// <summary>A poison message does not stop the rest of the batch from being processed.</summary>
        [Fact(DisplayName = "A poison message does not block the messages after it")]
        public async Task PoisonMessageDoesNotBlockFollowingMessages()
        {
            var factory = new FakeQueueClientProxyFactory();
            var options = new PersistentInboxOptions
            {
                QueueEndpoint = "https://fake.queue.core.windows.net",
                InboxQueueName = "inbox",
                ErrorQueueName = "errors",
                MaxDequeueCount = 1,
            };
            var inbox = new AzureQueueInbox(factory, options, NullLogger<AzureQueueInbox>.Instance);
            var inboxProxy = factory.GetProxy(options.InboxQueueName);

            await EnqueueRedeliveredAsync(inbox, inboxProxy, "poison", 2);
            await inbox.PublishMessageAsync(new TestIntegrationEvent("healthy"), CancellationToken.None);

            var read = await ReadAllAsync(inbox);

            var readBack = Assert.IsType<TestIntegrationEvent>(Assert.Single(read));
            Assert.Equal("healthy", readBack.Payload);
            Assert.Equal(2, inboxProxy.DeletedMessageIds.Count);
            Assert.Single(factory.GetProxy(options.ErrorQueueName).SentMessages);
        }

        /// <summary>A max dequeue count below one would reject every message, so it is refused.</summary>
        [Fact(DisplayName = "A max dequeue count below one is rejected")]
        public void MaxDequeueCountBelowOneIsRejected()
        {
            var options = new PersistentInboxOptions { QueueEndpoint = "e", InboxQueueName = "q", MaxDequeueCount = 0 };

            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new AzureQueueInbox(new FakeQueueClientProxyFactory(), options, NullLogger<AzureQueueInbox>.Instance));
            _ = Assert.Throws<ArgumentNullException>(() => new AzureQueueInbox(new FakeQueueClientProxyFactory(), null!, NullLogger<AzureQueueInbox>.Instance));
        }
    }
}
