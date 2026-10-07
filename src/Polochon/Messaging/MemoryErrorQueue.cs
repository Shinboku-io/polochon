using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;

namespace Polochon.Messaging
{
    /// <summary>
    /// In-memory, non-durable implementation of <see cref="IErrorQueue"/>: logs the failure and
    /// keeps it in memory for inspection (e.g. by tests), for the lifetime of the current process.
    /// Registered by default for every module, so a failing handler is always recorded somewhere,
    /// even for a module that never opts into a persistent inbox/error queue provider.
    /// </summary>
    internal sealed class MemoryErrorQueue : IErrorQueue
    {
        private readonly ConcurrentQueue<(IIntegrationEvent Message, Exception Exception)> failures = new();
        private readonly ILogger<MemoryErrorQueue> logger;

        public MemoryErrorQueue(ILogger<MemoryErrorQueue> logger)
        {
            this.logger = logger;
        }

        /// <summary>Every failure recorded so far, oldest first.</summary>
        public IReadOnlyCollection<(IIntegrationEvent Message, Exception Exception)> Failures => failures.ToArray();

        public ValueTask PublishFailedMessageAsync(IIntegrationEvent message, Exception exception, CancellationToken cancellationToken)
        {
            failures.Enqueue((message, exception));
            logger.LogError(exception, "Message {MessageType} ({MessageId}) moved to the error queue", message.GetType(), message.Id);

            return ValueTask.CompletedTask;
        }
    }
}
