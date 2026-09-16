using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    /// <summary>
    /// Polochon-specific mediator interface that wraps the underlying mediator library.
    /// This provides a clean abstraction over the Mediator library.
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
        ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command that doesn't return a value.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The command response.</returns>
        ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of IPolochonMediator that wraps the Mediator library.
    /// This hides the Mediator library implementation behind a clean abstraction.
    /// </summary>
    internal sealed class PolochonDispatcher : IPolochonDispatcher
    {
        private readonly IServiceProvider provider;

        private readonly DispatcherRegistry registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolochonDispatcher"/> class.
        /// </summary>
        /// <param name="mediator">The underlying Mediator instance.</param>
        public PolochonDispatcher(
            IServiceProvider provider,
            DispatcherRegistry registry)
        {
            this.provider = provider;
            this.registry = registry;
        }

        /// <inheritdoc/>
        public async ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (!registry.RequestWrappers.TryGetValue(query.GetType(), out var wrapper))
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type '{query.GetType().FullName}'.");
            }

            // Reference-type cast - cheap, no boxing.
            return await ((MessageHandlerBase<TResponse>)wrapper).HandleAsync(query, provider, cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!registry.RequestWrappers.TryGetValue(command.GetType(), out var wrapper))
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type '{command.GetType().FullName}'.");
            }

            // Reference-type cast - cheap, no boxing.
            await ((MessageHandlerBase<Unit>)wrapper).HandleAsync(command, provider, cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!registry.RequestWrappers.TryGetValue(command.GetType(), out var wrapper))
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type '{command.GetType().FullName}'.");
            }

            // Reference-type cast - cheap, no boxing.
            return await ((MessageHandlerBase<TResponse>)wrapper).HandleAsync(command, provider, cancellationToken);
        }
    }
}
