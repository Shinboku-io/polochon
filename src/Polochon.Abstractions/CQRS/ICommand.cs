namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Marker interface for command requests in CQRS pattern.
    /// </summary>
    public interface ICommand : Mediator.IRequest
    {
    }

    /// <summary>
    /// Marker interface for commands that return a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the command response.</typeparam>
    public interface ICommand<out TResponse> : Mediator.IRequest<TResponse>
    {
    }
}
