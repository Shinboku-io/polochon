namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Marker interface for query requests in CQRS pattern.
    /// </summary>
    /// <typeparam name="TResponse">The type of the query response.</typeparam>
    public interface IQuery<out TResponse> : IMessage<TResponse>
    {
    }
}
