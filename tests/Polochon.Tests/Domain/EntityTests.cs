using Polochon.Abstractions.Domain;
using Xunit;

namespace Polochon.Tests.Domain
{
    /// <summary>
    /// Tests for <see cref="Entity{TIdentifier}"/>: raising, holding, and clearing domain and
    /// integration events.
    /// </summary>
    public sealed class EntityTests
    {
        /// <summary>Raising a domain event adds it to <c>DomainEvents</c> only.</summary>
        [Fact]
        public void Raise_DomainEvent_AddsItToDomainEventsOnly()
        {
            var aggregate = new TestAggregate();
            var domainEvent = new TestDomainEvent("payload");

            aggregate.Raise(domainEvent);

            Assert.Same(domainEvent, Assert.Single(aggregate.DomainEvents));
            Assert.Empty(aggregate.IntegrationEvents);
        }

        /// <summary>Raising an integration event adds it to <c>IntegrationEvents</c> only.</summary>
        [Fact]
        public void Raise_IntegrationEvent_AddsItToIntegrationEventsOnly()
        {
            var aggregate = new TestAggregate();
            var integrationEvent = new TestIntegrationEvent("payload");

            aggregate.Raise(integrationEvent);

            Assert.Same(integrationEvent, Assert.Single(aggregate.IntegrationEvents));
            Assert.Empty(aggregate.DomainEvents);
        }

        /// <summary>Multiple raised events are kept in the order they were raised.</summary>
        [Fact]
        public void Raise_EventsAreKeptInRaisedOrder()
        {
            var aggregate = new TestAggregate();
            var first = new TestDomainEvent("first");
            var second = new TestDomainEvent("second");

            aggregate.Raise(first);
            aggregate.Raise(second);

            Assert.Equal([first, second], aggregate.DomainEvents);
        }

        /// <summary>
        /// An event implementing both <see cref="IDomainEvent"/> and <see cref="IIntegrationEvent"/>
        /// is rejected, since the two model different concerns that must not be coupled.
        /// </summary>
        [Fact]
        public void Raise_EventImplementingBothDomainAndIntegrationEvent_Throws()
        {
            var aggregate = new TestAggregate();

            var exception = Assert.Throws<InvalidOperationException>(() => aggregate.Raise(new DualPurposeTestEvent()));

            Assert.Contains(nameof(IDomainEvent), exception.Message);
            Assert.Contains(nameof(IIntegrationEvent), exception.Message);
            Assert.Empty(aggregate.DomainEvents);
            Assert.Empty(aggregate.IntegrationEvents);
        }

        /// <summary>
        /// <c>ClearDomainEvents</c> is reachable through an <see cref="IDomainEventSource"/>
        /// reference (it is implemented explicitly, so it isn't part of the aggregate's ordinary
        /// public surface).
        /// </summary>
        [Fact]
        public void ClearDomainEvents_ThroughInterfaceReference_EmptiesDomainEvents()
        {
            var aggregate = new TestAggregate();
            aggregate.Raise(new TestDomainEvent("payload"));

            ((IDomainEventSource)aggregate).ClearDomainEvents();

            Assert.Empty(aggregate.DomainEvents);
        }

        /// <summary>
        /// <c>ClearIntegrationEvents</c> is reachable through an <see cref="IIntegrationEventSource"/>
        /// reference (it is implemented explicitly, so it isn't part of the aggregate's ordinary
        /// public surface).
        /// </summary>
        [Fact]
        public void ClearIntegrationEvents_ThroughInterfaceReference_EmptiesIntegrationEvents()
        {
            var aggregate = new TestAggregate();
            aggregate.Raise(new TestIntegrationEvent("payload"));

            ((IIntegrationEventSource)aggregate).ClearIntegrationEvents();

            Assert.Empty(aggregate.IntegrationEvents);
        }

        /// <summary>Clearing one kind of event does not affect the other.</summary>
        [Fact]
        public void ClearDomainEvents_DoesNotAffectIntegrationEvents()
        {
            var aggregate = new TestAggregate();
            aggregate.Raise(new TestDomainEvent("d"));
            aggregate.Raise(new TestIntegrationEvent("i"));

            ((IDomainEventSource)aggregate).ClearDomainEvents();

            Assert.Empty(aggregate.DomainEvents);
            Assert.Single(aggregate.IntegrationEvents);
        }
    }
}
