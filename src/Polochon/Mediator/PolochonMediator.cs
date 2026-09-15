using Polochon.Abstractions.CQRS;

namespace Polochon.Mediator
{
    /// <summary>
    /// Implementation of IPolochonMediator that wraps the Mediator library.
    /// This hides the Mediator library implementation behind a clean abstraction.
    /// </summary>
    public sealed class PolochonMediator : IPolochonMediator
    {
        private readonly global::Mediator.IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolochonMediator"/> class.
        /// </summary>
        /// <param name="mediator">The underlying Mediator instance.</param>
        public PolochonMediator(global::Mediator.IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <inheritdoc/>
        public async Task<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            return await _mediator.Send((global::Mediator.IQuery<TResponse>)query, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            _ = await _mediator.Send((global::Mediator.ICommand)command, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            return await _mediator.Send((global::Mediator.ICommand<TResponse>)command, cancellationToken).ConfigureAwait(false);
        }
    }
}
