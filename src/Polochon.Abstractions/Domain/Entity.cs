using Polochon.Abstractions.CQRS;

namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// Base class for aggregate roots: identity, plus raising and holding domain and integration
    /// events until the unit of work collects them.
    /// </summary>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public abstract class Entity<TIdentifier> : IAggregateRoot<TIdentifier>, IDomainEventSource, IIntegrationEventSource
        where TIdentifier : notnull
    {
        /// <summary>
        /// Creates a new entity with the given identifier.
        /// </summary>
        /// <param name="identifier">The aggregate root's unique identifier.</param>
        protected Entity(TIdentifier identifier)
        {
            Identifier = identifier;
        }

        /// <inheritdoc/>
        public TIdentifier Identifier { get; }

        private readonly List<IDomainEvent> domainEvents = [];

        private readonly List<IIntegrationEvent> integrationEvents = [];

        /// <inheritdoc/>
        public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

        /// <inheritdoc/>
        public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents => integrationEvents.AsReadOnly();

        /// <summary>
        /// Raises a domain or integration event on this aggregate. A notification that implements
        /// both <see cref="IDomainEvent"/> and <see cref="IIntegrationEvent"/> is rejected - see the
        /// remarks on <see cref="IIntegrationEvent"/> for why.
        /// </summary>
        /// <param name="notification">The event to raise.</param>
        protected void AddEvent(INotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            switch (notification)
            {
                case IDomainEvent and IIntegrationEvent:
                    // Domain events model internal invariants; integration events are a versioned
                    // public contract for other modules. Allowing one type to be both means an
                    // internal refactor of the domain event becomes a breaking change for every
                    // other module subscribed to it - raise two distinct events instead.
                    throw new InvalidOperationException(
                        $"'{notification.GetType().FullName}' implements both {nameof(IDomainEvent)} and {nameof(IIntegrationEvent)}. A single event type must not be both - raise two distinct events instead.");

                case IDomainEvent domainEvent:
                    domainEvents.Add(domainEvent);
                    break;

                case IIntegrationEvent integrationEvent:
                    integrationEvents.Add(integrationEvent);
                    break;

                default:
                    break;
            }
        }

        void IDomainEventSource.ClearDomainEvents()
        {
            domainEvents.Clear();
        }

        void IIntegrationEventSource.ClearIntegrationEvents()
        {
            integrationEvents.Clear();
        }
    }
}
