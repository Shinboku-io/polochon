using System.Linq.Expressions;
using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Base class for specifications that filter, sort, and (optionally) page a result set.
    /// Builds on <see cref="FilterSpecification{TAggregateRoot, TIdentifier}"/> for the filtering
    /// predicate, adding <see cref="Skip"/>/<see cref="Take"/>/<see cref="SortClauses"/>.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root the specification applies to.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public abstract class PagedSpecification<TAggregateRoot, TIdentifier>
        : FilterSpecification<TAggregateRoot, TIdentifier>, IPagedSpecification<TAggregateRoot, TIdentifier>
        where TAggregateRoot : IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// Creates a specification filtering, sorting, and (optionally) paging aggregate roots.
        /// </summary>
        /// <param name="criteria">The predicate an aggregate root must satisfy to match. Defaults to matching everything.</param>
        /// <param name="sortClauses">The ordering to apply, in precedence order. Defaults to no ordering.</param>
        /// <param name="skip">The number of records to skip. Defaults to 0.</param>
        /// <param name="take">The number of records to take. Defaults to <see cref="int.MaxValue"/> (i.e. not truly paged).</param>
        protected PagedSpecification(
            Expression<Func<TAggregateRoot, bool>>? criteria = null,
            IReadOnlyList<ISortClause<TAggregateRoot>>? sortClauses = null,
            int skip = 0,
            int take = int.MaxValue)
            : base(criteria ?? (_ => true))
        {
            SortClauses = sortClauses ?? [];
            Skip = skip;
            Take = take;
        }

        /// <inheritdoc/>
        public int Skip { get; }

        /// <inheritdoc/>
        public int Take { get; }

        /// <inheritdoc/>
        public IReadOnlyList<ISortClause<TAggregateRoot>> SortClauses { get; }
    }
}
