using Microsoft.Extensions.Logging.Abstractions;
using Polochon.Messaging;
using Polochon.Tests.Domain;
using Xunit;

namespace Polochon.Tests.Messaging
{
    /// <summary>
    /// Tests for <see cref="MemoryErrorQueue"/>: the default, in-memory <c>IErrorQueue</c> every
    /// module gets out of the box.
    /// </summary>
    public sealed class MemoryErrorQueueTests
    {
        /// <summary>A published failure is recorded, along with the exception that caused it.</summary>
        [Fact]
        public async Task PublishFailedMessageAsync_RecordsMessageAndException()
        {
            var errorQueue = new MemoryErrorQueue(NullLogger<MemoryErrorQueue>.Instance);
            var message = new TestIntegrationEvent("payload");
            var exception = new InvalidOperationException("handler blew up");

            await errorQueue.PublishFailedMessageAsync(message, exception, CancellationToken.None);

            var failure = Assert.Single(errorQueue.Failures);
            Assert.Same(message, failure.Message);
            Assert.Same(exception, failure.Exception);
        }

        /// <summary>Failures accumulate in publish order.</summary>
        [Fact]
        public async Task PublishFailedMessageAsync_MultipleFailures_AreRecordedInOrder()
        {
            var errorQueue = new MemoryErrorQueue(NullLogger<MemoryErrorQueue>.Instance);
            var first = new TestIntegrationEvent("first");
            var second = new TestIntegrationEvent("second");

            await errorQueue.PublishFailedMessageAsync(first, new InvalidOperationException("one"), CancellationToken.None);
            await errorQueue.PublishFailedMessageAsync(second, new InvalidOperationException("two"), CancellationToken.None);

            Assert.Equal([first, second], errorQueue.Failures.Select(f => f.Message));
        }
    }
}
