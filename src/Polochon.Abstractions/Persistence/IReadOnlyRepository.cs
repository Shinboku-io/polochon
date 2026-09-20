using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Exposes read-only access to a store of aggregate roots.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root being read.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public interface IReadOnlyRepository<TAggregateRoot, TIdentifier>
        where TAggregateRoot : IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// Retrieves the aggregate root with the given identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the aggregate root to retrieve.</param>
        /// <returns>The matching aggregate root, or <see langword="null"/> if none is found.</returns>
        Task<TAggregateRoot?> GetByIdAsync(TIdentifier id);

        /// <summary>
        /// Retrieves the single aggregate root matching the given specification.
        /// </summary>
        /// <param name="specification">The specification describing which aggregate root to retrieve.</param>
        /// <returns>The matching aggregate root, or <see langword="null"/> if none is found.</returns>
        Task<TAggregateRoot?> GetAsync(ISpecification<TAggregateRoot, TIdentifier> specification);

        /// <summary>
        /// Runs an arbitrary query over the store, projecting the aggregate root to a custom result shape.
        /// </summary>
        /// <typeparam name="TResult">The type of the elements the query projects.</typeparam>
        /// <param name="query">The query describing how to build the projection.</param>
        /// <returns>Every element produced by the query.</returns>
        Task<IReadOnlyList<TResult>> QueryAsync<TResult>(IRepositoryQuery<TAggregateRoot, TResult> query);

        /// <summary>
        /// Retrieves a single page of aggregate roots matching the given specification.
        /// </summary>
        /// <param name="pagedSpecification">The specification describing which aggregate roots to retrieve and how to page them.</param>
        /// <returns>The requested page of matching aggregate roots, along with the total number of matching elements.</returns>
        Task<PagedResult<TAggregateRoot>> ListAsync(IPagedSpecification<TAggregateRoot, TIdentifier> pagedSpecification);

        /// <summary>
        /// Retrieves every aggregate root matching the given specification.
        /// </summary>
        /// <param name="specification">The specification describing which aggregate roots to retrieve.</param>
        /// <returns>The matching aggregate roots.</returns>
        Task<IReadOnlyList<TAggregateRoot>> ListAsync(ISpecification<TAggregateRoot, TIdentifier> specification);
    }
}