using Polochon.Abstractions.CQRS;

namespace Polochon.Abstractions.Modules
{
    /// <summary>
    /// Represents a modular module in a modular monolith architecture.
    /// Each module has its own isolated service container and mediator instance.
    /// </summary>
    public interface IModularModule : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the singleton instance of the module.
        /// </summary>
        static virtual IModularModule? Instance => null;

        /// <summary>
        /// Gets the module's internal service provider.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Sends a query and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="query">The query to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The query response.</returns>
        ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command that doesn't return a value.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The command response.</returns>
        ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes the module with its own service container.
        /// </summary>
        /// <param name="configuration">Optional configuration for the module.</param>
        void Initialize(IModuleConfiguration? configuration = null);

        /// <summary>
        /// Initializes the module asynchronously with its own service container.
        /// </summary>
        /// <param name="configuration">Optional configuration for the module.</param>
        Task InitializeAsync(IModuleConfiguration? configuration = null);
    }

    /// <summary>
    /// Marker interface for module configuration.
    /// </summary>
    public interface IModuleConfiguration
    {
    }
}
