namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Represents a query that supports pagination, allowing the caller to specify how many records to skip and take.
    /// </summary>
    public interface IPagedQuery
    {
        /// <summary>
        /// The number of records to skip
        /// </summary>
        int Skip { get; }

        /// <summary>
        /// The number of records to take ; default 10
        /// </summary>
        int Take { get; }
    }
}