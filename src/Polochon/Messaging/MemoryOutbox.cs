using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;

namespace Polochon.Messaging
{
    /// <summary>
    /// In-memory, non-durable implementation of <see cref="IOutbox"/>. See the remarks on
    /// <see cref="IOutbox"/>: this is a relay, not a transactional outbox - buffered messages do
    /// not survive a process restart.
    /// </summary>
    internal sealed class MemoryOutbox : IOutbox
    {
        private readonly Channel<IIntegrationEvent> channel = Channel.CreateUnbounded<IIntegrationEvent>();

        public async IAsyncEnumerable<IIntegrationEvent> DrainPendingMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (channel.Reader.TryRead(out var message))
            {
                yield return message;
            }

            await Task.CompletedTask;
        }

        public ValueTask PublishMessageAsync(IIntegrationEvent message, CancellationToken cancellationToken)
        {
            return channel.Writer.WriteAsync(message, cancellationToken);
        }
    }
}
