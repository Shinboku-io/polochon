using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    internal interface INotificationHandlerWrapper
    {
        ValueTask HandleAsync(
            object notification,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    internal sealed class NotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
        where TNotification : INotification
    {
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