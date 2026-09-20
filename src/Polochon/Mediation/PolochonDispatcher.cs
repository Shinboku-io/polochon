using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    /// <inheritdoc />
    internal sealed class PolochonDispatcher : IPolochonDispatcher
    {
        private readonly IServiceProvider provider;

        private readonly DispatcherRegistry registry;

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

            if (!registry.QueryWrappers.TryGetValue(query.GetType(), out var wrapper))
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type '{query.GetType().FullName}'.");
            }

            // Reference-type cast - cheap, no boxing.
            return await ((IMessageHandlerWrapper<TResponse>)wrapper).HandleAsync(query, provider, cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!registry.CommandWrappers.TryGetValue(command.GetType(), out var wrapper))
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type '{command.GetType().FullName}'.");
            }

            // Reference-type cast - cheap, no boxing.
            _ = await ((IMessageHandlerWrapper<Unit>)wrapper).HandleAsync(command, provider, cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!registry.CommandWrappers.TryGetValue(command.GetType(), out var wrapper))
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type '{command.GetType().FullName}'.");
            }

            // Reference-type cast - cheap, no boxing.
            return await ((IMessageHandlerWrapper<TResponse>)wrapper).HandleAsync(command, provider, cancellationToken);
        }
    }
}
