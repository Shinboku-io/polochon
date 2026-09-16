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

        /// <summary>
        /// Gets the query handlers registry.
        /// </summary>
        public FrozenDictionary<Type, IMessageHandlerWrapper> QueryWrappers { get; }

        /// <summary>
        /// Gets the command handlers registry.
        /// </summary>
        public FrozenDictionary<Type, IMessageHandlerWrapper> CommandWrappers { get; }

        /// <summary>
        /// Gets the notification handlers registry.
        /// </summary>
        public FrozenDictionary<Type, INotificationHandlerWrapper> NotificationWrappers { get; }
    }
}
