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
    }
}
