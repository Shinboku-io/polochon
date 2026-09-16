namespace Polochon.Abstractions.CQRS
{
    public delegate ValueTask<TResponse> MessageHandlerDelegate<TResponse>();

    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        ValueTask<TResponse> HandleAsync(
            TRequest request,
            MessageHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }
}