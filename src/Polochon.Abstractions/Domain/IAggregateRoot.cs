namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// Marker interface for domain aggregate roots.
    /// An aggregate root is the single entry point through which a cluster of related
    /// domain objects (an aggregate) is loaded, mutated and persisted as one consistency boundary.
    /// Repositories are defined per aggregate root, never for the entities within it.
    /// </summary>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public interface IAggregateRoot<out TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// The unique identifier of the aggregate root.
        /// </summary>
        TIdentifier Identifier { get; }
    }
}