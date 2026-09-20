using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;

namespace Polochon.Tests.Domain
{
    /// <summary>
    /// Minimal <see cref="Entity{TIdentifier}"/> subclass exposing <c>AddEvent</c> publicly so
    /// tests can raise events without needing a full aggregate.
    /// </summary>
    public class TestAggregate : Entity<Guid>
    {
        /// <summary>Creates an aggregate with a random identifier.</summary>
        public TestAggregate()
            : base(Guid.NewGuid())
        {
        }

        /// <summary>Creates an aggregate with the given identifier.</summary>
        public TestAggregate(Guid id)
            : base(id)
        {
        }

        /// <summary>Raises the given notification, delegating to the protected <c>AddEvent</c>.</summary>
        public void Raise(INotification notification) => AddEvent(notification);
    }

    /// <summary>
    /// A standalone aggregate that raises a domain event when deleted, to exercise
    /// <see cref="IRaiseEventOnDelete"/>.
    /// </summary>
    public sealed class DeletableTestAggregate : Entity<Guid>, IRaiseEventOnDelete
    {
        /// <summary>Creates an aggregate with a random identifier.</summary>
        public DeletableTestAggregate()
            : base(Guid.NewGuid())
        {
        }

        /// <summary>Whether <see cref="OnDelete"/> has been called.</summary>
        public bool OnDeleteWasCalled { get; private set; }

        /// <summary>Raises the given notification, delegating to the protected <c>AddEvent</c>.</summary>
        public void Raise(INotification notification) => AddEvent(notification);

        /// <inheritdoc/>
        public void OnDelete()
        {
            OnDeleteWasCalled = true;
            Raise(new TestDomainEvent("deleted"));
        }
    }
}
