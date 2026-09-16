
using System.Collections.Frozen;

namespace Polochon.Mediation
{
    /// <summary>
    /// Built once at startup, frozen for the lifetime of the application. Singleton.
    /// </summary>
    internal sealed class DispatcherRegistry(
        FrozenDictionary<Type, MessageHandlerBase> requestWrappers,
        FrozenDictionary<Type, NotificationHandlerBase> notificationWrappers)
    {
        public FrozenDictionary<Type, MessageHandlerBase> RequestWrappers { get; } = requestWrappers;
        public FrozenDictionary<Type, NotificationHandlerBase> NotificationWrappers { get; } = notificationWrappers;
    }
}