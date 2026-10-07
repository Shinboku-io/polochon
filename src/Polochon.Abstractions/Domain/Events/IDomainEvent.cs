using Polochon.Abstractions.CQRS;

namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// A fact about a change to an aggregate's internal state, dispatched in-process, in the same
    /// unit of work, before the change is persisted. Models internal invariants and reactions -
    /// not a contract for other modules. See <see cref="IIntegrationEvent"/> for that.
    /// </summary>
    public interface IDomainEvent : INotification
    {
    }
}
