using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Modules;
using Polochon.Mediator;

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
        public IPolochonMediator Mediator => ServiceProvider.GetRequiredService<IPolochonMediator>();

        /// <inheritdoc/>
        public Task<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
            => Mediator.SendQueryAsync<TResponse>(query, cancellationToken);

        /// <inheritdoc/>
        public Task SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
            => Mediator.SendCommandAsync(command, cancellationToken);

        /// <inheritdoc/>
        public Task<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => Mediator.SendCommandAsync<TResponse>(command, cancellationToken);

        /// <summary>
        /// Configures the services for this module.
        /// Override this method to register module-specific services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        protected virtual void ConfigureServices([DisallowNull] IServiceCollection services, IReadOnlyList<Assembly> types)
        {
            if (!TryConfigureGeneratedMediatorServices(services, types))
            {
                // Register the Mediator library and its source-generated handlers
                _ = services.AddMediator((global::Mediator.MediatorOptions options) =>
                {
                    options.GenerateTypesAsInternal = true;
                    options.ServiceLifetime = ServiceLifetime.Singleton;
                    options.Assemblies = types.Select(x => (global::Mediator.AssemblyReference)x).ToList().AsReadOnly();
                });
            }

            // Register our Polochon mediator adapter
            services.AddSingleton<IPolochonMediator, PolochonMediator>();
        }

        /// <summary>
        /// Attempts to invoke a generated module-specific Mediator configuration helper.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="types">The module assemblies to scan.</param>
        /// <returns><see langword="true" /> when a generated helper was found and invoked; otherwise <see langword="false" />.</returns>
        protected virtual bool TryConfigureGeneratedMediatorServices(IServiceCollection services, IReadOnlyList<Assembly> types)
        {
            var moduleType = GetType();
            var helperTypeName = string.IsNullOrWhiteSpace(moduleType.Namespace)
                ? $"{moduleType.Name}MediatorConfiguration"
                : $"{moduleType.Namespace}.{moduleType.Name}MediatorConfiguration";

            var helperType = moduleType.Assembly.GetType(helperTypeName, throwOnError: false, ignoreCase: false);
            if (helperType is null)
            {
                return false;
            }

            var configureMethod = helperType.GetMethod(
                "ConfigureMediatorServices",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(IServiceCollection), typeof(IReadOnlyList<Assembly>)],
                modifiers: null);

            if (configureMethod is null)
            {
                return false;
            }

            configureMethod.Invoke(null, [services, types]);
            return true;
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
