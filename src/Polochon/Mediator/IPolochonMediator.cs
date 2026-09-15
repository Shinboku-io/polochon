using Polochon.Abstractions.CQRS;

namespace Polochon.Mediator
{
    /// <summary>
    /// Polochon-specific mediator interface that wraps the underlying mediator library.
    /// This provides a clean abstraction over the Mediator library.
    /// </summary>
    public interface IPolochonMediator
    {
        /// <summary>
        /// Sends a query and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="query">The query to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The query response.</returns>
        Task<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command that doesn't return a value.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task SendCommandAsync(ICommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The command response.</returns>
        Task<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
    }
}
