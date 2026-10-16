using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;

namespace Polochon.Messaging
{
    /// <summary>
    /// The <see cref="IInboxWriter"/> a module exposes to the cross-module <see cref="EventBus"/>
    /// (see <c>ModuleBase.Inbox</c>): delegates to the module's actual inbox - in-memory or
    /// persistent - then raises the module's <see cref="InboxSignal"/>, so an in-process
    /// <see cref="InboxProcessor"/> wakes up without polling.
    /// </summary>
    internal sealed class SignalingInboxWriter : IInboxWriter
    {
        private readonly IInboxWriter inner;
        private readonly InboxSignal signal;

        public SignalingInboxWriter(IInboxWriter inner, InboxSignal signal)
        {
            this.inner = inner;
            this.signal = signal;
        }

        public async ValueTask PublishMessageAsync(IIntegrationEvent message, CancellationToken cancellationToken)
        {
            await inner.PublishMessageAsync(message, cancellationToken);

            // Only once the message is actually in the inbox: a processor woken earlier could
            // drain before the message lands, and then wait for the next delivery to see it.
            signal.Notify();
        }
    }
}
