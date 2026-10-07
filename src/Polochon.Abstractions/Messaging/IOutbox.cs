using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Messaging
{
    /// <summary>
    /// Producer side of a module's outbox: buffers an integration event for later delivery.
    /// Depend on this - not <see cref="IOutboxReader"/> or <see cref="IOutbox"/> - from code that
    /// only ever needs to publish (e.g. a unit of work), so it cannot accidentally drain the
    /// module's own outbox.
    /// </summary>
    public interface IOutboxWriter
    {
        /// <summary>
        /// Buffers an integration event for later delivery via <see cref="IOutboxReader.DrainPendingMessagesAsync"/>.
        /// </summary>
        ValueTask PublishMessageAsync(IIntegrationEvent message, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Bus side of a module's outbox: drains buffered integration events for relay to other
    /// modules. Depend on this - not <see cref="IOutboxWriter"/> or <see cref="IOutbox"/> - from
    /// code that only ever needs to relay messages out (e.g. the cross-module event bus), so it
    /// cannot accidentally publish into the module's own outbox.
    /// </summary>
    public interface IOutboxReader
    {
        /// <summary>
        /// Drains the integration events currently buffered in memory. This is a consuming read:
        /// once returned, a message is removed from the relay. It does not wait for messages
        /// published after the call starts - a caller that needs continuous delivery must poll.
        /// </summary>
        IAsyncEnumerable<IIntegrationEvent> DrainPendingMessagesAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// In-process relay for integration events awaiting delivery to other modules.
    /// </summary>
    /// <remarks>
    /// By design, this relay is not durable: messages live only in memory for the lifetime of the
    /// current process and are lost on crash or restart. Durable, at-least-once delivery is the
    /// responsibility of each consuming module's own inbox (planned, not yet implemented): the
    /// future cross-module bus drains this relay and hands each message off to the target module's
    /// inbox, which is what actually guarantees the message survives (e.g. by triggering that
    /// module's pod to start from a durable input queue). Do not depend on this type where losing a
    /// buffered message on restart would be unacceptable - that guarantee only exists once a
    /// message has reached a module's inbox.
    /// </remarks>
    public interface IOutbox : IOutboxWriter, IOutboxReader
    {
    }
}
