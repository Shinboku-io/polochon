namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Marker for an in-process notification. Multiple handlers per notification are allowed.
    /// </summary>
    public interface INotification
    {
        /// <summary>
        /// A stable identity for this occurrence, independent of <see cref="OccurredAt"/>.
        /// Carried on the base notification - rather than added later only to integration events -
        /// so that cross-module consumers can dedupe or replay by id without every event author
        /// having to retrofit one in once the cross-module bus exists.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// When this notification occurred.
        /// </summary>
        DateTimeOffset OccurredAt { get; }
    }
}
