namespace Polochon.Abstractions.CQRS
{
public interface ISender
{
    ValueTask<TResponse> Send<TResponse>(
        IQuery<TResponse> request,
        CancellationToken cancellationToken = default);
}
}