using System.Linq.Expressions;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// A strongly-typed <see cref="ISortClause{T}"/> ordering elements by a single key.
    /// </summary>
    /// <typeparam name="T">The type of the elements being ordered.</typeparam>
    /// <typeparam name="TKey">The type of the ordering key.</typeparam>
    public sealed class SortClause<T, TKey> : ISortClause<T>
    {
        private readonly Expression<Func<T, TKey>> keySelector;
        private readonly bool ascending;

        /// <summary>
        /// Creates a new sort clause.
        /// </summary>
        /// <param name="keySelector">The expression selecting the property to sort by.</param>
        /// <param name="ascending">Whether to sort in ascending order; <see langword="false"/> for descending.</param>
        public SortClause(Expression<Func<T, TKey>> keySelector, bool ascending = true)
        {
            this.keySelector = keySelector;
            this.ascending = ascending;
        }

        /// <inheritdoc/>
        public IOrderedQueryable<T> Apply(IQueryable<T> query) =>
            ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);

        /// <inheritdoc/>
        public IOrderedQueryable<T> ThenApply(IOrderedQueryable<T> query) =>
            ascending ? query.ThenBy(keySelector) : query.ThenByDescending(keySelector);
    }
}
