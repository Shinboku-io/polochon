using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;

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
        /// Determines whether the module can handle the specified integration event, i.e. whether
        /// it has at least one registered handler for it. Used for inbox routing: unlike commands
        /// and queries, integration events are pub/sub, so this does not guarantee the module is
        /// the only one that can handle it.
        /// </summary>
        /// <param name="integrationEvent">The integration event to check.</param>
        /// <returns>True if the module has at least one handler for the event; otherwise, false.</returns>
        bool CanHandleIntegrationEvent(IIntegrationEvent integrationEvent);

        /// <summary>
        /// The delivery point for integration events published by other modules. Only exposes the
        /// write side: only the cross-module event bus is expected to deliver messages here.
        /// </summary>
        IInboxWriter Inbox { get; }

        /// <summary>
        /// The relay of integration events awaiting delivery to other modules. Only exposes the
        /// read side: only the cross-module event bus is expected to drain messages from here.
        /// </summary>
        IOutboxReader Outbox { get; }

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