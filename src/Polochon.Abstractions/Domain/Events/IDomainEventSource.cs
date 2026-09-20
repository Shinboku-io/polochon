namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// Exposes the domain events an aggregate has raised since they were last cleared.
    /// </summary>
    public interface IDomainEventSource
    {
        /// <summary>
        /// The domain events raised since they were last cleared, in the order they were raised.
        /// </summary>
        IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

        /// <summary>
        /// Clears the raised domain events. Infrastructure-only: called by the unit of work once
        /// the events have been collected for dispatch. <see cref="Entity{TIdentifier}"/> implements
        /// this explicitly so it isn't part of an aggregate's ordinary public surface - reach it
        /// through an <see cref="IDomainEventSource"/> reference, which is what the persistence
        /// layer holds via the change tracker.
        /// </summary>
        void ClearDomainEvents();
    }
}
