namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Handler interface for processing notifications.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification asynchronously.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
    }
}
