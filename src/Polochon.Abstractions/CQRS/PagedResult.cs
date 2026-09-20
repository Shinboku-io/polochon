namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Represents a paged result of a query, containing the current page of items and the total number of elements.
    /// </summary>
    /// <typeparam name="T">The type of the items in the current page.</typeparam>
    /// <param name="CurrentPage">The current page of items.</param>
    /// <param name="TotalElements">The total number of elements matching the query.</param>
    public record PagedResult<T>(IReadOnlyList<T> CurrentPage, int TotalElements);
}