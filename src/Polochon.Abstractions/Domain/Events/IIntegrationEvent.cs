using Polochon.Abstractions.CQRS;

namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// A versioned, published fact meant for other modules. Published to the outbox only after
    /// the owning transaction has committed. Must not also be an <see cref="IDomainEvent"/>: the
    /// two model different concerns and changing one must not force a breaking change onto the
    /// other. See <see cref="Entity{TIdentifier}.AddEvent"/>, which enforces this.
    /// </summary>
    public interface IIntegrationEvent : INotification
    {
    }
}
