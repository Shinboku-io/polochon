using System.Collections.Frozen;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    public static class ServiceRegister
    {

        /// <summary>
        /// Registers the dispatcher and scans the given assembly for IRequestHandler and
        /// INotificationHandler implementations. Wrappers are built once and frozen.
        /// </summary>
        public static IServiceCollection AddDispatcher(this IServiceCollection services, Assembly assembly)
        {
            var requestWrappers = new Dictionary<Type, MessageHandlerBase>();
            var notificationWrappers = new Dictionary<Type, NotificationHandlerBase>();

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType) continue;
                    var def = iface.GetGenericTypeDefinition();

                    if (def == typeof(IMessageHandler<,>))
                    {
                        _ = services.AddScoped(iface, type);

                        var args = iface.GetGenericArguments();
                        var requestType = args[0];
                        var responseType = args[1];

                        if (!requestWrappers.ContainsKey(requestType))
                        {
                            var wrapperType = typeof(MessageHandlerWrapper<,>)
                                .MakeGenericType(requestType, responseType);
                            requestWrappers[requestType] =
                                (MessageHandlerBase)Activator.CreateInstance(wrapperType)!;
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
                                (NotificationHandlerBase)Activator.CreateInstance(wrapperType)!;
                        }
                    }
                }
            }

            var registry = new DispatcherRegistry(
                requestWrappers.ToFrozenDictionary(),
                notificationWrappers.ToFrozenDictionary());

            _ = services.AddSingleton(registry);
            _ = services.AddScoped<PolochonDispatcher>();
            services.AddScoped<IPolochonDispatcher>(sp => sp.GetRequiredService<PolochonDispatcher>());
            //services.AddScoped<IPublisher>(sp => sp.GetRequiredService<Dispatcher>());

            return services;
        }

        /// <summary>
        /// Registers an open-generic pipeline behavior. Order of registration is order of execution.
        /// </summary>
        public static IServiceCollection AddPipelineBehavior(
            this IServiceCollection services,
            Type openGenericBehaviorType)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), openGenericBehaviorType);
            return services;
        }
    }
}