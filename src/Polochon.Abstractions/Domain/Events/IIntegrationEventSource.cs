namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// Exposes the integration events an aggregate has raised since they were last cleared.
    /// </summary>
    public interface IIntegrationEventSource
    {
        /// <summary>
        /// The integration events raised since they were last cleared, in the order they were raised.
        /// </summary>
        IReadOnlyCollection<IIntegrationEvent> IntegrationEvents { get; }

        /// <summary>
        /// Clears the raised integration events. Infrastructure-only: called by the unit of work
        /// once the events have been collected for publishing. <see cref="Entity{TIdentifier}"/>
        /// implements this explicitly so it isn't part of an aggregate's ordinary public surface -
        /// reach it through an <see cref="IIntegrationEventSource"/> reference, which is what the
        /// persistence layer holds via the change tracker.
        /// </summary>
        void ClearIntegrationEvents();
    }
}
