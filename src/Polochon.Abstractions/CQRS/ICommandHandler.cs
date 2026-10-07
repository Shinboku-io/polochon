using Polochon.Abstractions.Results;

namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Handler interface for processing an <see cref="ICommand"/>. Alias of
    /// <see cref="ICommandHandler{TCommand, TResponse}"/> of <see cref="CommandResult"/>, discovered
    /// by the same assembly scan.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, CommandResult>
        where TCommand : ICommand
    {
    }

    /// <summary>
    /// Handler interface for processing commands that return a response.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the command response.</typeparam>
    public interface ICommandHandler<in TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        /// <summary>
        /// Handles the command asynchronously.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The command response.</returns>
        ValueTask<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}
