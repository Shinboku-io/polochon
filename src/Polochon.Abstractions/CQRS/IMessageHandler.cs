namespace Polochon.Abstractions.CQRS
{

    public interface IMessageHandler<in TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
    }
}