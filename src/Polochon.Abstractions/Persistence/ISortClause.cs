namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// A single, strongly-typed ordering criterion to apply when materializing a query.
    /// Implementations close over the concrete key type so the ordering expression stays
    /// compiler-checked, while the actual sort translation is left entirely to the
    /// underlying <see cref="IQueryable{T}"/> provider (e.g. an EF Core provider).
    /// </summary>
    /// <typeparam name="T">The type of the elements being ordered.</typeparam>
    public interface ISortClause<T>
    {
        /// <summary>
        /// Applies this clause as the primary ordering of the given query.
        /// </summary>
        /// <param name="query">The query to order.</param>
        /// <returns>The ordered query.</returns>
        IOrderedQueryable<T> Apply(IQueryable<T> query);

        /// <summary>
        /// Applies this clause as a secondary ordering on top of an already ordered query.
        /// </summary>
        /// <param name="query">The already ordered query.</param>
        /// <returns>The ordered query.</returns>
        IOrderedQueryable<T> ThenApply(IOrderedQueryable<T> query);
    }
}
