namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Handler interface for processing queries.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the query response.</typeparam>
    public interface IQueryHandler<in TQuery, TResponse> : IMessageHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {

    }
}
