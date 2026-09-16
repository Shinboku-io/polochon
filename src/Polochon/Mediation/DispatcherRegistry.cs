using System.Collections.Frozen;

namespace Polochon.Mediation
{
    /// <summary>
    /// Built once at startup, frozen for the lifetime of the application. Singleton.
    /// </summary>
    internal sealed class DispatcherRegistry
    {
        public DispatcherRegistry(
            FrozenDictionary<Type, IMessageHandlerWrapper> queryWrappers,
            FrozenDictionary<Type, IMessageHandlerWrapper> commandWrappers,
            FrozenDictionary<Type, INotificationHandlerWrapper> notificationWrappers)
        {
            CommandWrappers = commandWrappers;
            QueryWrappers = queryWrappers;
            NotificationWrappers = notificationWrappers;
        }

        public FrozenDictionary<Type, IMessageHandlerWrapper> QueryWrappers { get; }

        public FrozenDictionary<Type, IMessageHandlerWrapper> CommandWrappers { get; }

        public FrozenDictionary<Type, INotificationHandlerWrapper> NotificationWrappers { get; }
    }
}