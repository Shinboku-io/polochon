namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Handler interface for processing messages.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IMessageHandler<in TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        /// <summary>
        /// Handles the message asynchronously.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
    }
}