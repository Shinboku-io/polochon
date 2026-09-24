using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Exposes read and write access to a store of aggregate roots.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root being read and written.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public interface IMutableRepository<TAggregateRoot, TIdentifier> : IReadOnlyRepository<TAggregateRoot, TIdentifier>
        where TAggregateRoot : IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// Adds a new aggregate root to the store.
        /// </summary>
        /// <param name="entity">The aggregate root to add.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The added aggregate root.</returns>
        Task<TAggregateRoot> AddAsync(TAggregateRoot entity, CancellationToken cancellationToken);

        /// <summary>
        /// Adds several new aggregate roots to the store.
        /// </summary>
        /// <param name="entities">The aggregate roots to add.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The added aggregate roots.</returns>
        Task<TAggregateRoot[]> AddRangeAsync(TAggregateRoot[] entities, CancellationToken cancellationToken);

        /// <summary>
        /// Removes an aggregate root from the store.
        /// </summary>
        /// <param name="entity">The aggregate root to remove.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task DeleteAsync(TAggregateRoot entity, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing aggregate root in the store.
        /// </summary>
        /// <param name="entity">The aggregate root to update.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated aggregate root.</returns>
        Task<TAggregateRoot> UpdateAsync(TAggregateRoot entity, CancellationToken cancellationToken);
    }
}