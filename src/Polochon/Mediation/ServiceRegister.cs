using System.Collections.Frozen;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Messaging;

namespace Polochon.Mediation
{
    /// <summary>
    /// Provides extension methods for registering the Polochon dispatcher and related services.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers the dispatcher and scans the given handler types for IRequestHandler and
        /// INotificationHandler implementations. Wrappers are built once and frozen.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        /// <param name="handlerTypes">The candidate types to scan for handler implementations.</param>
        public static IServiceCollection AddDispatcher(this IServiceCollection services, params Type[] handlerTypes)
        {
            var registry = RegisterWrappersAndTypes(services, handlerTypes);

            return services.AddDispatcherCore(registry);
        }

        /// <summary>
        /// Registers the dispatcher and scans the given assembly for IRequestHandler and
        /// INotificationHandler implementations. Wrappers are built once and frozen.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        /// <param name="assembly">The assembly to scan for handler implementations.</param>
        public static IServiceCollection AddDispatcher(this IServiceCollection services, params Assembly[] assembly)
        {
            var registry = RegisterWrappersAndTypes(services, [.. assembly.SelectMany(a => a.GetTypes())]);

            return services.AddDispatcherCore(registry);
        }

        private static IServiceCollection AddDispatcherCore(this IServiceCollection services, DispatcherRegistry registry)
        {
            _ = services.AddSingleton(registry);
            _ = services.AddScoped<PolochonDispatcher>();
            _ = services.AddScoped<IPolochonDispatcher>(sp => sp.GetRequiredService<PolochonDispatcher>());
            _ = services.AddScoped<INotificationPublisher, NotificationPublisher>();
            _ = services.AddSingleton<IOutbox, MemoryOutbox>();
            _ = services.AddSingleton<IOutboxWriter>(sp => sp.GetRequiredService<IOutbox>());
            _ = services.AddSingleton<IOutboxReader>(sp => sp.GetRequiredService<IOutbox>());
            _ = services.AddSingleton<IInbox, MemoryInbox>();
            _ = services.AddSingleton<IInboxWriter>(sp => sp.GetRequiredService<IInbox>());
            _ = services.AddSingleton<IInboxReader>(sp => sp.GetRequiredService<IInbox>());
            _ = services.AddSingleton<IErrorQueue, MemoryErrorQueue>();

            return services;
        }

        private static DispatcherRegistry RegisterWrappersAndTypes(
            IServiceCollection services,
            Type[] types)
        {
            var queryWrappers = new Dictionary<Type, IMessageHandlerWrapper>();
            var commandWrappers = new Dictionary<Type, IMessageHandlerWrapper>();
            var notificationWrappers = new Dictionary<Type, INotificationHandlerWrapper>();
            var queryTypes = new HashSet<Type>();
            var commandTypes = new HashSet<Type>();

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;

                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType) continue;
                    var def = iface.GetGenericTypeDefinition();

                    if (def == typeof(IQueryHandler<,>))
                    {
                        _ = services.AddScoped(iface, type);

                        var args = iface.GetGenericArguments();
                        var requestType = args[0];
                        var responseType = args[1];

                        _ = queryTypes.Add(requestType);

                        if (!queryWrappers.ContainsKey(requestType))
                        {
                            var wrapperType = typeof(QueryHandlerWrapper<,>)
                                .MakeGenericType(requestType, responseType);
                            queryWrappers[requestType] =
                                (IMessageHandlerWrapper)Activator.CreateInstance(wrapperType)!;
                        }
                    }
                    else if (def == typeof(ICommandHandler<,>))
                    {
                        _ = services.AddScoped(iface, type);

                        var args = iface.GetGenericArguments();
                        var requestType = args[0];
                        var responseType = args[1];

                        _ = commandTypes.Add(requestType);

                        if (!commandWrappers.ContainsKey(requestType))
                        {
                            var wrapperType = typeof(CommandHandlerWrapper<,>)
                                .MakeGenericType(requestType, responseType);
                            commandWrappers[requestType] =
                                (IMessageHandlerWrapper)Activator.CreateInstance(wrapperType)!;
                        }
                    }
                    else if (def == typeof(INotificationHandler<>))
                    {
                        services.AddScoped(iface, type);

                        var notificationType = iface.GetGenericArguments()[0];
                        if (!notificationWrappers.ContainsKey(notificationType))
                        {
                            var wrapperType = typeof(NotificationHandlerWrapper<>)
                                .MakeGenericType(notificationType);
                            notificationWrappers[notificationType] =
                                (INotificationHandlerWrapper)Activator.CreateInstance(wrapperType)!;
                        }
                    }
                    else if (def == typeof(IMessageValidator<,>))
                    {
                        _ = services.AddScoped(iface, type);
                    }
                }
            }

            return new DispatcherRegistry(
                queryTypes.ToFrozenSet(),
                commandTypes.ToFrozenSet(),
                queryWrappers.ToFrozenDictionary(),
                commandWrappers.ToFrozenDictionary(),
                notificationWrappers.ToFrozenDictionary());
        }

        /// <summary>
        /// Registers an open-generic pipeline behavior. Order of registration is order of execution.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        /// <param name="openGenericBehaviorType">The open-generic pipeline behavior type.</param>
        public static IServiceCollection AddPipelineBehavior(
            this IServiceCollection services,
            Type openGenericBehaviorType)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), openGenericBehaviorType);
            return services;
        }
    }
}