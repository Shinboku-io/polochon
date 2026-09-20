using System.Linq.Expressions;
using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Base class for specifications whose only concern is a filtering predicate.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root the specification applies to.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public abstract class FilterSpecification<TAggregateRoot, TIdentifier> : ISpecification<TAggregateRoot, TIdentifier>
        where TAggregateRoot : IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
        private readonly Expression<Func<TAggregateRoot, bool>> criteria;

        /// <summary>
        /// Creates a specification matching aggregate roots satisfying the given criteria.
        /// </summary>
        /// <param name="criteria">The predicate an aggregate root must satisfy to match.</param>
        protected FilterSpecification(Expression<Func<TAggregateRoot, bool>> criteria)
        {
            this.criteria = criteria;
        }

        /// <inheritdoc/>
        public IQueryable<TAggregateRoot> BuildQuery(IQueryable<TAggregateRoot> source) => source.Where(criteria);
    }
}