namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Delegate for message handler operations.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public delegate ValueTask<TResponse> MessageHandlerDelegate<TResponse>();

    /// <summary>
    /// Pipeline behavior interface for wrapping message handlers.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        /// <summary>
        /// Executes the pipeline behavior asynchronously.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="next">The next handler in the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        ValueTask<TResponse> HandleAsync(
            TRequest request,
            MessageHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }
}
