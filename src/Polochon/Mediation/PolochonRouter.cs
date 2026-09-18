using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Modules;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Polochon.Mediation
{
    internal class PolochonRouter : IPolochonDispatcher
    {
        private readonly ConcurrentDictionary<Type, IModularModule> moduleMapping = new ConcurrentDictionary<Type, IModularModule>();

        private readonly List<IModularModule> modules = [];

        public PolochonRouter(IEnumerable<IModularModule> modules)
        {
            this.modules.AddRange(modules);
        }

        public ValueTask SendCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            // Already found
            if (moduleMapping.TryGetValue(command.GetType(), out var module))
            {
                return module.SendCommandAsync(command, cancellationToken);
            }

            // find mapping
            var foundModule = modules.FirstOrDefault(m => m.CanHandleCommand(command));

            if (foundModule != null)
            {
                _ = moduleMapping.AddOrUpdate(command.GetType(), foundModule, (key, oldValue) => foundModule);
                return foundModule.SendCommandAsync(command, cancellationToken);
            }

            throw new InvalidOperationException($"No module found to handle command of type {command.GetType().Name}");
        }

        public ValueTask<TResponse> SendCommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            // Already found
            if (moduleMapping.TryGetValue(command.GetType(), out var module))
            {
                return module.SendCommandAsync(command, cancellationToken);
            }

            // find mapping
            var foundModule = modules.FirstOrDefault(m => m.CanHandleCommand(command));

            if (foundModule != null)
            {
                _ = moduleMapping.AddOrUpdate(command.GetType(), foundModule, (key, oldValue) => foundModule);
                return foundModule.SendCommandAsync(command, cancellationToken);
            }

            throw new InvalidOperationException($"No module found to handle command of type {command.GetType().Name}");
        }

        public ValueTask<TResponse> SendQueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            // Already found
            if (moduleMapping.TryGetValue(query.GetType(), out var module))
            {
                return module.SendQueryAsync(query, cancellationToken);
            }

            // find mapping
            var foundModule = modules.FirstOrDefault(m => m.CanHandleQuery(query));

            if (foundModule != null)
            {
                _ = moduleMapping.AddOrUpdate(query.GetType(), foundModule, (key, oldValue) => foundModule);
                return foundModule.SendQueryAsync(query, cancellationToken);
            }

            throw new InvalidOperationException($"No module found to handle query of type {query.GetType().Name}");
        }
    }
}