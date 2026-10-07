using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;

namespace Polochon.Messaging
{
    /// <summary>
    /// In-memory, non-durable implementation of <see cref="IInbox"/>. See the remarks on
    /// <see cref="IOutbox"/>: like the outbox relay it fronts, this is not a durable inbox -
    /// buffered messages do not survive a process restart.
    /// </summary>
    internal sealed class MemoryInbox : IInbox
    {
        private readonly Channel<IIntegrationEvent> channel = Channel.CreateUnbounded<IIntegrationEvent>();

        public async IAsyncEnumerable<IIntegrationEvent> ReadAllMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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
