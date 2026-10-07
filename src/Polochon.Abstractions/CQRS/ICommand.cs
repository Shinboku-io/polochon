using Polochon.Abstractions.Results;

namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Marker interface for command requests in CQRS pattern: a command reporting its outcome as a
    /// <see cref="CommandResult"/>. Alias of <see cref="ICommand{TResponse}"/> of
    /// <see cref="CommandResult"/> - there are no commands without a result.
    /// </summary>
    public interface ICommand : ICommand<CommandResult>
    {
    }

    /// <summary>
    /// Marker interface for commands that return a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the command response.</typeparam>
    public interface ICommand<out TResponse> : IMessage<TResponse>
    {
    }
}
