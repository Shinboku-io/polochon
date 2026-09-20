using Polochon.Abstractions.Domain;
using Polochon.Messaging;
using Polochon.Tests.Domain;
using Xunit;

namespace Polochon.Tests.Messaging
{
    /// <summary>
    /// Tests for <see cref="MemoryOutbox"/>: a non-durable, consuming, in-memory relay for
    /// integration events (see the remarks on <see cref="Polochon.Abstractions.Messaging.IOutbox"/>).
    /// </summary>
    public sealed class MemoryOutboxTests
    {
        /// <summary>Draining an empty outbox yields nothing, and does not hang.</summary>
        [Fact]
        public async Task DrainPendingMessagesAsync_WhenEmpty_YieldsNothing()
        {
            var outbox = new MemoryOutbox();

            var drained = await DrainAsync(outbox);

            Assert.Empty(drained);
        }

        /// <summary>Draining is a consuming read: a message is gone once returned.</summary>
        [Fact]
        public async Task DrainPendingMessagesAsync_ReturnsPublishedMessage_AndRemovesIt()
        {
            var outbox = new MemoryOutbox();
            var message = new TestIntegrationEvent("payload");

            await outbox.PublishMessageAsync(message, CancellationToken.None);

            var firstDrain = await DrainAsync(outbox);
            Assert.Same(message, Assert.Single(firstDrain));

            var secondDrain = await DrainAsync(outbox);
            Assert.Empty(secondDrain);
        }

        /// <summary>Messages are drained in the order they were published (FIFO).</summary>
        [Fact]
        public async Task DrainPendingMessagesAsync_ReturnsMessages_InPublishOrder()
        {
            var outbox = new MemoryOutbox();
            var first = new TestIntegrationEvent("first");
            var second = new TestIntegrationEvent("second");

            await outbox.PublishMessageAsync(first, CancellationToken.None);
            await outbox.PublishMessageAsync(second, CancellationToken.None);

            var drained = await DrainAsync(outbox);

            Assert.Equal([first, second], drained);
        }

        private static async Task<List<IIntegrationEvent>> DrainAsync(MemoryOutbox outbox)
        {
            var result = new List<IIntegrationEvent>();
            await foreach (var message in outbox.DrainPendingMessagesAsync(CancellationToken.None))
            {
                result.Add(message);
            }

            return result;
        }
    }
}
