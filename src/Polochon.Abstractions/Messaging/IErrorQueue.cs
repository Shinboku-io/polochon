using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Messaging
{
    /// <summary>
    /// Destination for an integration event that failed during inbox processing - the "dead
    /// letter" counterpart to <see cref="IInbox"/>. Every module gets a default, in-memory
    /// implementation out of the box (see <c>MemoryErrorQueue</c>), so an event whose handler
    /// throws is recorded instead of silently dropped, even without opting into a persistent inbox
    /// like an Azure Storage Queue-backed one.
    /// </summary>
    /// <remarks>
    /// Write-only by design: nothing in the kernel itself needs to read failures back - that's an
    /// operational concern for whoever inspects the underlying queue/store. A provider that also
    /// wants to route its own transport-level failures here (e.g. a message that could not even be
    /// deserialized) is free to do so through its own mechanism; this interface only covers a
    /// message that was read successfully but whose processing then failed.
    /// </remarks>
    public interface IErrorQueue
    {
        /// <summary>
        /// Records that <paramref name="message"/> failed during processing, with
        /// <paramref name="exception"/> as the cause.
        /// </summary>
        /// <param name="message">The integration event that was being processed.</param>
        /// <param name="exception">The exception raised while processing <paramref name="message"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        ValueTask PublishFailedMessageAsync(IIntegrationEvent message, Exception exception, CancellationToken cancellationToken);
    }
}
