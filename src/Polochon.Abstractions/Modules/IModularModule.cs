using Polochon.Abstractions.CQRS;

namespace Polochon.Abstractions.Modules
{
    /// <summary>
    /// Represents a modular module in a modular monolith architecture.
    /// Each module has its own isolated service container and mediator instance.
    /// </summary>
    public interface IModularModule : IDisposable, IAsyncDisposable, IPolochonDispatcher
    {
        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Determines whether the module can handle the specified command. Used for module routing
        /// </summary>
        /// <typeparam name="TResponse">The type of the response expected from the command.</typeparam>
        /// <param name="command">The command to check.</param>
        /// <returns>True if the module can handle the command; otherwise, false.</returns>
        bool CanHandleCommand<TResponse>(ICommand<TResponse> command);

        /// <summary>
        /// Determines whether the module can handle the specified query. Used for module routing.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response expected from the query.</typeparam>
        /// <param name="query">The query to check.</param>
        /// <returns>True if the module can handle the query; otherwise, false.</returns>
        bool CanHandleQuery<TResponse>(IQuery<TResponse> query);

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