using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Modules;
using Polochon.Logging;
using Polochon.Mediation;
using Polochon.Security;
using Polochon.Telemetry;

namespace Polochon.Modules
{
    /// <summary>
    /// Base class for modular monolith modules.
    /// Each module has its own isolated service container and mediator instance.
    /// </summary>
    public abstract class ModuleBase : IModularModule
    {
        private readonly ServiceCollection serviceCollection;
        private readonly List<Action<IServiceCollection, ModuleBase>> externalConfigurators = [];
        private IServiceProvider? serviceProvider;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleBase"/> class.
        /// </summary>
        /// <param name="moduleName">The name of the module.</param>
        /// <param name="types">Assemblies where business logic for mediation is defined. For mediation configuration.</param>
        protected ModuleBase(string moduleName, Assembly[] types)
        {
            Name = moduleName;
            serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, types);
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public IServiceProvider ServiceProvider
        {
            get
            {
                if (serviceProvider is null)
                {
                    throw new InvalidOperationException($"Module '{Name}' has not been initialized. Call Initialize() first.");
                }
                return serviceProvider;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Handled in a DI scope of its own, disposed once the query completes: its handler gets fresh
        /// scoped services (unit of work, <c>DbContext</c>...) instead of sharing them with every other
        /// message sent to this module. Messages a handler sends through its own injected
        /// <see cref="IPolochonDispatcher"/> stay in that scope.
        /// </remarks>
        public async ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IPolochonDispatcher>().SendQueryAsync(query, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>Handled in a DI scope of its own - see <see cref="SendQueryAsync{TResponse}"/>.</remarks>
        public async ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPolochonDispatcher>().SendCommandAsync(command, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>Handled in a DI scope of its own - see <see cref="SendQueryAsync{TResponse}"/>.</remarks>
        public async ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IPolochonDispatcher>().SendCommandAsync(command, cancellationToken);
        }

        /// <inheritdoc/>
        public bool CanHandleCommand<TResponse>(ICommand<TResponse> command)
        {
            var registry = ServiceProvider.GetRequiredService<DispatcherRegistry>();
            return registry.CommandTypes.Contains(command.GetType());
        }

        /// <inheritdoc/>
        public bool CanHandleQuery<TResponse>(IQuery<TResponse> query)
        {
            var registry = ServiceProvider.GetRequiredService<DispatcherRegistry>();
            return registry.QueryTypes.Contains(query.GetType());
        }

        /// <inheritdoc/>
        public bool CanHandleIntegrationEvent(IIntegrationEvent integrationEvent)
        {
            var registry = ServiceProvider.GetRequiredService<DispatcherRegistry>();
            return registry.NotificationWrappers.ContainsKey(integrationEvent.GetType());
        }

        /// <inheritdoc/>
        public IInboxWriter Inbox => GetRequiredService<IInboxWriter>();

        /// <inheritdoc/>
        public IOutboxReader Outbox => GetRequiredService<IOutboxReader>();

        /// <summary>
        /// Configures the services for this module.
        /// Override this method to register module-specific services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="types">Assemblies where business logic for mediation is defined. For mediation configuration.</param>
        protected virtual void ConfigureServices([DisallowNull] IServiceCollection services, IReadOnlyList<Assembly> types)
        {
            _ = services.AddDispatcher(types.ToArray());

            // Registered first so its behavior runs outermost: traces and times the whole pipeline,
            // and sees the final CommandResult - including failures CommandResultBehavior converts.
            _ = services.AddPolochonTelemetry(Name);

            // Registered next so it wraps everything else: commands returning a CommandResult report
            // every failure, including one thrown by the unit of work, as a ResultCode.
            _ = services.AddPipelineBehavior(typeof(CommandResultBehavior<,>));
            _ = services.AddPolochonLogging();
            _ = services.AddPolochonUserIdentity();
        }

        /// <summary>
        /// Configures additional services after the base configuration.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        protected virtual void ConfigureAdditionalServices([DisallowNull] IServiceCollection services)
        {
            // Override in derived modules to add additional services
        }

        /// <summary>
        /// Queues a callback that configures this module's isolated service collection. Called by
        /// <c>AddModule&lt;TModule&gt;()</c> to apply configuration layered on by an <see cref="IModularModuleBuilder"/>
        /// (e.g. from a Polochon extension package such as Polochon.Serilog).
        /// </summary>
        /// <param name="configure">A callback that configures the module's isolated service collection, given the module instance.</param>
        internal void AddConfigurator(Action<IServiceCollection, ModuleBase> configure) => externalConfigurators.Add(configure);

        /// <inheritdoc/>
        public void Initialize(IModuleConfiguration? configuration = null)
        {
            ConfigureAdditionalServices(serviceCollection);
            ApplyExternalConfigurators();
            serviceProvider = serviceCollection.BuildServiceProvider();
        }

        /// <inheritdoc/>
        public async Task InitializeAsync(IModuleConfiguration? configuration = null)
        {
            ConfigureAdditionalServices(serviceCollection);
            ApplyExternalConfigurators();
            serviceProvider = serviceCollection.BuildServiceProvider();
            await OnInitializedAsync().ConfigureAwait(false);
        }

        private void ApplyExternalConfigurators()
        {
            foreach (var configure in externalConfigurators)
            {
                configure(serviceCollection, this);
            }
        }

        /// <summary>
        /// Called after the module is initialized.
        /// Override this method to perform async initialization logic.
        /// </summary>
        /// <returns>A task representing the async operation.</returns>
        protected virtual Task OnInitializedAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a service of the specified type from the module's service provider.
        /// </summary>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <returns>The service instance.</returns>
        [return: MaybeNull]
        public T GetService<T>()
            => ServiceProvider.GetService<T>();

        /// <summary>
        /// Gets a required service of the specified type from the module's service provider.
        /// </summary>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not found or the module is not initialized.</exception>
        public T GetRequiredService<T>()
            where T : notnull
            => ServiceProvider.GetRequiredService<T>();

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the module resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }
            }

            disposed = true;
        }

        /// <summary>
        /// Disposes async resources.
        /// </summary>
        /// <returns>A task representing the async operation.</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (serviceProvider is IAsyncDisposable asyncDisposableProvider)
            {
                await asyncDisposableProvider.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}