using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Messaging
{
    /// <summary>
    /// Base for a notification handler that reacts to an integration event by translating it into
    /// a command and dispatching it within this module - the common way of consuming a
    /// cross-module integration event in a .NET CQRS modular monolith, instead of acting on the
    /// event directly inside the handler. Keeping translation (<see cref="Map"/>) separate from
    /// dispatch means every write - whether triggered by a user action or by another module's
    /// event - goes through the same command pipeline (validators, pipeline behaviors): the
    /// module's single front door.
    /// </summary>
    /// <remarks>
    /// Register a derived class the same way as any other <see cref="INotificationHandler{TNotification}"/> -
    /// nothing else needs to change: it is discovered by the same assembly scan (<c>AddDispatcher</c>),
    /// wrapped by the same <c>NotificationHandlerWrapper</c>, and counted by
    /// <c>IModularModule.CanHandleIntegrationEvent</c> for inbox routing, because it implements
    /// <see cref="INotificationHandler{TNotification}"/> like any other handler - this base class
    /// only standardizes what a derived handler does once invoked.
    /// </remarks>
    /// <typeparam name="TEvent">The integration event this handler reacts to.</typeparam>
    /// <typeparam name="TCommand">The command <see cref="Map"/> translates the event into.</typeparam>
    public abstract class IntegrationEventToCommandHandler<TEvent, TCommand> : INotificationHandler<TEvent>
        where TEvent : IIntegrationEvent
        where TCommand : ICommand
    {
        private readonly IPolochonDispatcher dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/> class.
        /// </summary>
        /// <param name="dispatcher">Used to send the mapped command through this module's own dispatcher.</param>
        protected IntegrationEventToCommandHandler(IPolochonDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        /// <summary>
        /// Translates the integration event into the command to dispatch. Keep this a pure
        /// mapping - no I/O, no business rules - that belongs in the command's own handler, not
        /// here.
        /// </summary>
        /// <param name="integrationEvent">The event to translate.</param>
        /// <returns>The command to dispatch.</returns>
        protected abstract TCommand Map(TEvent integrationEvent);

        /// <summary>
        /// Explicit implementation: dispatch is fixed by this base class, so a derived class can
        /// only ever supply <see cref="Map"/> - it cannot override how (or whether) the mapped
        /// command gets sent.
        /// </summary>
        ValueTask INotificationHandler<TEvent>.Handle(TEvent notification, CancellationToken cancellationToken)
            => dispatcher.SendCommandAsync(Map(notification), cancellationToken);
    }
}
