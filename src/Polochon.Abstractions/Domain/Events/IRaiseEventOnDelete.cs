namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// Opt-in hook for aggregates that need to raise an event when they are deleted. Called by the
    /// unit of work while the entity is still tracked as <c>Deleted</c>, before it is collected as
    /// a domain/integration event source, so events raised here are dispatched/published normally.
    /// </summary>
    public interface IRaiseEventOnDelete
    {
        /// <summary>
        /// Called while this entity is still tracked as deleted, so it can raise its deletion
        /// event(s) via <c>AddEvent</c> before the change tracker forgets it.
        /// </summary>
        void OnDelete();
    }
}
