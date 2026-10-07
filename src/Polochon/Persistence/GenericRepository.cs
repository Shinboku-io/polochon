using System.Linq.Expressions;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Polochon.Persistence
{
    /// <summary>
    /// EF Core backed implementation of <see cref="IMutableRepository{TAggregateRoot, TIdentifier}"/>,
    /// generic over any aggregate root.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate root persisted by this repository.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public class GenericRepository<T, TIdentifier> : IMutableRepository<T, TIdentifier>
        where T : class, IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
        private readonly DbSet<T> set;

        private readonly Expression<Func<T, TIdentifier>> idSelector;

        /// <summary>
        /// Creates a new repository over the given <see cref="DbContext"/>.
        /// </summary>
        /// <param name="context">The database context exposing the aggregate root's <see cref="DbSet{TEntity}"/>.</param>
        /// <param name="idSelector">
        /// Selects <typeparamref name="T"/>'s mapped identifier property, e.g. <c>item => item.Id</c>.
        /// Used by <see cref="GetByIdAsync"/> to build an EF-translatable equality predicate.
        /// Must reference the concrete mapped property directly: <see cref="IAggregateRoot{TIdentifier}.Identifier"/>
        /// is deliberately not used for this, since aggregate roots implement it explicitly and EF Core
        /// cannot translate a query against an interface member that isn't part of its model.
        /// </param>
        public GenericRepository(DbContext context, Expression<Func<T, TIdentifier>> idSelector)
        {
            set = context.Set<T>();
            this.idSelector = idSelector;
        }

        /// <summary>
        /// Applies eager-loading (e.g. <c>Include</c>) to the base queryable before a specification's
        /// criteria/sorting are applied. Override in a derived, EF-aware repository to eagerly load
        /// navigation properties that must always be present to reconstitute <typeparamref name="T"/>
        /// as a valid aggregate root. Specifications stay provider-agnostic on purpose (see
        /// <see cref="ISpecification{T, TIdentifier}"/>); this is the seam where EF-specific query
        /// shaping belongs instead.
        /// Not applied to <see cref="QueryAsync{TResult}"/>, whose caller fully controls the query
        /// shape, nor to <see cref="DbSet{TEntity}.Local"/> lookups, since <c>Include</c> requires an
        /// EF query provider and <c>Local</c> is LINQ-to-Objects.
        /// </summary>
        /// <param name="query">The base queryable over <typeparamref name="T"/>.</param>
        /// <returns>The queryable with any required eager-loading applied. The default implementation applies none.</returns>
        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query) => query;

        /// <inheritdoc/>
        public Task<T?> GetByIdAsync(TIdentifier id, CancellationToken cancellationToken)
        {
            var equals = Expression.Equal(idSelector.Body, Expression.Constant(id, typeof(TIdentifier)));
            var predicate = Expression.Lambda<Func<T, bool>>(equals, idSelector.Parameters);
            return GetAsync(new IdEqualsSpecification(predicate), cancellationToken);
        }

        private sealed class IdEqualsSpecification : FilterSpecification<T, TIdentifier>
        {
            public IdEqualsSpecification(Expression<Func<T, bool>> predicate)
                : base(predicate)
            {
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<TResult>> QueryAsync<TResult>(IRepositoryQuery<T, TResult> query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var built = query.BuildQuery(set.AsQueryable());

            return await built.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T, TIdentifier> specification, CancellationToken cancellationToken)
        {
            var filteredQuery = specification.BuildQuery(ApplyIncludes(set.AsQueryable()));
            var result = await filteredQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

            // Include local entities that match the specification and are not already in the result.
            // LINQ-to-Objects only, since Local doesn't support EF's async query translation.
            var localFiltered = specification.BuildQuery(set.Local.AsQueryable());
            var localResults = localFiltered.Where(local => !result.Any(r => EqualityComparer<TIdentifier>.Default.Equals(r.Identifier, local.Identifier)));

            return [.. result, .. localResults];
        }

        /// <inheritdoc/>
        public async Task<PagedResult<T>> ListAsync(IPagedSpecification<T, TIdentifier> pagedSpecification, CancellationToken cancellationToken)
        {
            var query = pagedSpecification.BuildQuery(ApplyIncludes(set.AsQueryable()));

            int totalElements = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            // Apply sorting: the first clause is the primary sort key, subsequent clauses break ties.
            IOrderedQueryable<T>? ordered = null;
            foreach (var clause in pagedSpecification.SortClauses)
            {
                ordered = ordered is null ? clause.Apply(query) : clause.ThenApply(ordered);
            }

            IQueryable<T> sortedQuery = ordered ?? query;

            var result = await sortedQuery
                .Skip(pagedSpecification.Skip)
                .Take(pagedSpecification.Take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new PagedResult<T>(result, totalElements);
        }

        /// <inheritdoc/>
        public async Task<T?> GetAsync(ISpecification<T, TIdentifier> specification, CancellationToken cancellationToken)
        {
            var filteredQuery = specification.BuildQuery(ApplyIncludes(set.AsQueryable()));

            var data = await filteredQuery.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (data is not null)
            {
                return data;
            }

            // Check local cache for entities not yet saved to the database.
            // LINQ-to-Objects only, since Local doesn't support EF's async query translation.
            var localFiltered = specification.BuildQuery(set.Local.AsQueryable());
            return localFiltered.FirstOrDefault();
        }

        /// <inheritdoc/>
        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken)
        {
            _ = await set.AddAsync(entity, cancellationToken);
            return entity;
        }

        /// <inheritdoc/>
        public async Task<T[]> AddRangeAsync(T[] entities, CancellationToken cancellationToken)
        {
            await set.AddRangeAsync(entities, cancellationToken);
            return entities;
        }

        /// <inheritdoc/>
        public Task DeleteAsync(T entity, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            _ = set.Remove(entity);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<T> UpdateAsync(T entity, CancellationToken cancellationToken)
        {
            var entry = set.Entry(entity);

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<T>(cancellationToken);
            }

            if (entry.State == EntityState.Unchanged)
            {
                entry.State = EntityState.Modified;
            }

            return Task.FromResult(entity);
        }
    }
}