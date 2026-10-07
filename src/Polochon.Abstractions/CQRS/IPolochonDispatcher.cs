namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Represents a dispatcher that can send queries and commands in a CQRS architecture.
    /// </summary>
    public interface IPolochonDispatcher
    {
        /// <summary>
        /// Sends a query and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="query">The query to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The query response.</returns>
        /// <exception cref="MessageValidationException">Thrown when a validator rejects the message; the handler is not called.</exception>
        ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command and returns the response. An <see cref="ICommand"/> returns its
        /// <see cref="Results.CommandResult"/>.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The command response.</returns>
        /// <exception cref="MessageValidationException">Thrown when a validator rejects the message; the handler is not called.</exception>
        ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
    }
}