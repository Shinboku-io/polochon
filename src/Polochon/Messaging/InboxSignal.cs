using System.Threading.Channels;

namespace Polochon.Messaging
{
    /// <summary>
    /// Per-module wake-up signal raised whenever the cross-module <see cref="EventBus"/> delivers a
    /// message into the module's inbox (see <see cref="SignalingInboxWriter"/>). Lets an
    /// <see cref="InboxProcessor"/> in <see cref="InboxProcessingMode.InProcess"/> mode read the
    /// inbox only when something was actually delivered, instead of on a fixed clock.
    /// </summary>
    /// <remarks>
    /// Coalescing by design: it only records that at least one delivery happened since the last
    /// wait, not how many - the processor drains the whole inbox on every wake-up anyway. A
    /// delivery raised while a drain is in progress stays pending, so the next wait returns at once
    /// and nothing delivered mid-drain is left waiting for another delivery.
    /// </remarks>
    internal sealed class InboxSignal
    {
        private readonly Channel<bool> channel = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        /// <summary>Records that a message was delivered into the inbox. Never blocks.</summary>
        public void Notify() => _ = channel.Writer.TryWrite(true);

        /// <summary>Completes once a delivery has been recorded since the previous wait, consuming it.</summary>
        /// <param name="cancellationToken">Cancels the wait without consuming a pending delivery.</param>
        public async ValueTask WaitAsync(CancellationToken cancellationToken)
            => _ = await channel.Reader.ReadAsync(cancellationToken);
    }
}
