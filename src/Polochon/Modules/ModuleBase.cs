using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Modules;
using Polochon.Mediation;

namespace Polochon.Modules
{
    /// <summary>
    /// Base class for modular monolith modules.
    /// Each module has its own isolated service container and mediator instance.
    /// </summary>
    public abstract class ModuleBase : IModularModule
    {
        private readonly ServiceCollection _serviceCollection;
        private IServiceProvider? _serviceProvider;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleBase"/> class.
        /// </summary>
        /// <param name="moduleName">The name of the module.</param>
        /// <param name="types">Assemblies where business logic for mediation is defined. For mediation configuration.</param>
        protected ModuleBase(string moduleName, Assembly[] types)
        {
            Name = moduleName;
            _serviceCollection = new ServiceCollection();
            ConfigureServices(_serviceCollection, types);
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider is null)
                {
                    throw new InvalidOperationException($"Module '{Name}' has not been initialized. Call Initialize() first.");
                }
                return _serviceProvider;
            }
        }

        /// <summary>
        /// Gets the module's mediator instance.
        /// </summary>
        public IPolochonDispatcher Mediator => ServiceProvider.GetRequiredService<IPolochonDispatcher>();

        /// <inheritdoc/>
        public ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
            => Mediator.SendQueryAsync<TResponse>(query, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
            => Mediator.SendCommandAsync(command, cancellationToken);

        /// <inheritdoc/>
        public ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => Mediator.SendCommandAsync<TResponse>(command, cancellationToken);

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

        /// <summary>
        /// Configures the services for this module.
        /// Override this method to register module-specific services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="types">Assemblies where business logic for mediation is defined. For mediation configuration.</param>
        protected virtual void ConfigureServices([DisallowNull] IServiceCollection services, IReadOnlyList<Assembly> types)
        {
            services.AddDispatcher(types.First());
        }

        /// <summary>
        /// Configures additional services after the base configuration.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        protected virtual void ConfigureAdditionalServices([DisallowNull] IServiceCollection services)
        {
            // Override in derived modules to add additional services
        }

        /// <inheritdoc/>
        public void Initialize(IModuleConfiguration? configuration = null)
        {
            ConfigureAdditionalServices(_serviceCollection);
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        /// <inheritdoc/>
        public async Task InitializeAsync(IModuleConfiguration? configuration = null)
        {
            ConfigureAdditionalServices(_serviceCollection);
            _serviceProvider = _serviceCollection.BuildServiceProvider();
            await OnInitializedAsync().ConfigureAwait(false);
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
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Disposes async resources.
        /// </summary>
        /// <returns>A task representing the async operation.</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_serviceProvider is IAsyncDisposable asyncDisposableProvider)
            {
                await asyncDisposableProvider.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}