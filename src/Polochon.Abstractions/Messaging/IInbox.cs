using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Messaging
{
    /// <summary>
    /// Bus side of a module's inbox: hands an integration event delivered from another module's
    /// outbox off to this module. Depend on this - not <see cref="IInboxReader"/> or
    /// <see cref="IInbox"/> - from code that only ever needs to deliver messages in (e.g. the
    /// cross-module event bus), so it cannot accidentally consume the module's own inbox.
    /// </summary>
    public interface IInboxWriter
    {
        /// <summary>
        /// Buffers an integration event delivered from another module, for later processing via
        /// <see cref="IInboxReader.ReadAllMessagesAsync"/>.
        /// </summary>
        ValueTask PublishMessageAsync(IIntegrationEvent message, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Consumer side of a module's inbox: reads integration events delivered from other modules so
    /// this module can react to them. Depend on this - not <see cref="IInboxWriter"/> or
    /// <see cref="IInbox"/> - from code that only ever needs to consume its own inbox (e.g. the
    /// module's own inbox-processing loop), so it cannot accidentally publish into it as if it were
    /// another module's bus delivery.
    /// </summary>
    public interface IInboxReader
    {
        /// <summary>
        /// Reads the integration events currently buffered in memory. This is a consuming read:
        /// once returned, a message is removed from the inbox. It does not wait for messages
        /// delivered after the call starts - a caller that needs continuous processing must poll.
        /// </summary>
        IAsyncEnumerable<IIntegrationEvent> ReadAllMessagesAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// A module's inbox: the delivery point for integration events published by other modules'
    /// outboxes.
    /// </summary>
    public interface IInbox : IInboxWriter, IInboxReader
    {
    }
}
