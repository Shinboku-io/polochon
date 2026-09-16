namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Interface for sending queries and commands.
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Sends a query and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The query to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The query response.</returns>
        ValueTask<TResponse> Send<TResponse>(
            IQuery<TResponse> request,
            CancellationToken cancellationToken = default);
    }
}
