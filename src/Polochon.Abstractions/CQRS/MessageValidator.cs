namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Base for message validators, whatever the library behind them. Owns the kernel's reporting
    /// rule, which subclasses cannot change: every rule is evaluated, and the first failure (in rule
    /// order) is the one reported to the caller, as its <see cref="ValidationError.Code"/>. All
    /// failures stay available on <see cref="MessageValidationException.Errors"/> for diagnostics.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message to validate.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public abstract class MessageValidator<TRequest, TResponse> : IMessageValidator<TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        /// <inheritdoc/>
        /// <remarks>Not virtual: the reporting rule is the kernel's, not the validator's.</remarks>
        public async ValueTask ValidateAsync(TRequest request, CancellationToken cancellationToken)
        {
            var errors = await GetErrorsAsync(request, cancellationToken);

            if (errors.Count > 0)
            {
                throw new MessageValidationException(errors);
            }
        }

        /// <summary>
        /// Evaluates every rule against <paramref name="request"/> and returns all the failures, in
        /// rule order. An empty list means the request is valid.
        /// </summary>
        /// <param name="request">The request message to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Every failure, in rule order.</returns>
        protected abstract ValueTask<IReadOnlyList<ValidationError>> GetErrorsAsync(TRequest request, CancellationToken cancellationToken);
    }
}
