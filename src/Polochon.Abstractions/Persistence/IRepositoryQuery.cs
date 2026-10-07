namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Describes how to shape a queryable source into a queryable result, e.g. by applying
    /// filtering, ordering, includes or projections.
    /// </summary>
    /// <typeparam name="TInput">The type of the elements in the source queryable.</typeparam>
    /// <typeparam name="TResult">The type of the elements in the resulting queryable.</typeparam>
    public interface IRepositoryQuery<TInput, TResult>
    {
        /// <summary>
        /// Applies this query's shaping logic to the given source.
        /// </summary>
        /// <param name="source">The queryable to shape.</param>
        /// <returns>The shaped queryable.</returns>
        IQueryable<TResult> BuildQuery(IQueryable<TInput> source);
    }
}
