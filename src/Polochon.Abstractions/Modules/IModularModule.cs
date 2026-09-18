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

        bool CanHandleCommand<TResponse>(ICommand<TResponse> command);

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