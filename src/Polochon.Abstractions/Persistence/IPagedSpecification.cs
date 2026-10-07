using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// A specification that additionally describes how to page its matching aggregate roots.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate root the specification applies to.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public interface IPagedSpecification<T, TIdentifier> : ISpecification<T, TIdentifier>
        where T : IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// The number of records to skip
        /// </summary>
        int Skip { get; }

        /// <summary>
        /// The number of records to take ; default 10
        /// </summary>
        int Take { get; }

        /// <summary>
        /// The ordering to apply to the matching aggregate roots, in precedence order
        /// (the first clause is the primary sort key, subsequent clauses break ties).
        /// </summary>
        IReadOnlyList<ISortClause<T>> SortClauses { get; }
    }
}
