using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    /// <summary>
    /// Marker interface for notification handler wrappers.
    /// </summary>
    internal interface INotificationHandlerWrapper
    {
        /// <summary>
        /// Handles the notification asynchronously.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="provider">The service provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        ValueTask HandleAsync(
            object notification,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Wrapper for notification handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    internal sealed class NotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification asynchronously by invoking all registered handlers.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="provider">The service provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async ValueTask HandleAsync(
            object notification,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            var typed = (TNotification)notification;
            var handlers = provider.GetServices<INotificationHandler<TNotification>>();

            // Sequential await - simple and predictable. Swap for Task.WhenAll if you want parallel.
            foreach (var handler in handlers)
            {
                await handler.Handle(typed, cancellationToken);
            }
        }
    }
}
