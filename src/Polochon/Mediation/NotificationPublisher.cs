using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    /// <inheritdoc />
    internal sealed class NotificationPublisher : INotificationPublisher
    {
        private readonly IServiceProvider provider;

        private readonly DispatcherRegistry registry;

        public NotificationPublisher(
            IServiceProvider provider,
            DispatcherRegistry registry)
        {
            this.provider = provider;
            this.registry = registry;
        }

        /// <inheritdoc/>
        public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : INotification
        {
            ArgumentNullException.ThrowIfNull(@event);

            if (!registry.NotificationWrappers.TryGetValue(@event.GetType(), out var wrapper))
            {
                // Notifications are pub/sub: having zero subscribers is a normal, valid state
                // (e.g. an event nobody consumes yet, or one only consumed by another module
                // later). Unlike commands/queries, publishing must not fail just because nobody
                // is listening right now.
                return;
            }

            await wrapper.HandleAsync(@event, provider, cancellationToken);
        }
    }
}
