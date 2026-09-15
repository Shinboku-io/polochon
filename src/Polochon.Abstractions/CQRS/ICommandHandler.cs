namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Handler interface for processing commands.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    public interface ICommandHandler<in TCommand>
        where TCommand : ICommand
    {
        /// <summary>
        /// Handles the command asynchronously.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
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
        Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}
