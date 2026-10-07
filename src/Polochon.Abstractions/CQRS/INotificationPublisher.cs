namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Publishes domain and integration events to their in-process subscribers.
    /// Unlike the commands and queries sent through <see cref="IPolochonDispatcher"/>, a
    /// notification may have zero, one, or many handlers - publishing succeeds even when nobody
    /// is listening.
    /// </summary>
    public interface INotificationPublisher
    {
        /// <summary>
        /// Publishes a notification to its in-process subscribers, if any.
        /// </summary>
        /// <typeparam name="TEvent">The type of the notification.</typeparam>
        /// <param name="event">The notification to publish.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
           where TEvent : INotification;
    }
}
