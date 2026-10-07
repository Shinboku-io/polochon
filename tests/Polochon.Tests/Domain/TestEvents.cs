using Polochon.Abstractions.Domain;

namespace Polochon.Tests.Domain
{
    /// <summary>
    /// A plain domain event used to exercise <see cref="Entity{TIdentifier}.AddEvent"/>.
    /// </summary>
    public sealed record TestDomainEvent : IDomainEvent
    {
        /// <summary>Creates the event with the given payload.</summary>
        public TestDomainEvent(string payload)
        {
            Payload = payload;
        }

        /// <summary>An arbitrary marker value used to identify this event in assertions.</summary>
        public string Payload { get; init; }

        /// <inheritdoc/>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <inheritdoc/>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// A second, distinct domain event type - used to test that a handler reacting to
    /// <see cref="TestDomainEvent"/> can raise a further event that still gets collected.
    /// </summary>
    public sealed record CascadedDomainEvent : IDomainEvent
    {
        /// <summary>Creates the event with the given payload.</summary>
        public CascadedDomainEvent(string payload)
        {
            Payload = payload;
        }

        /// <summary>An arbitrary marker value used to identify this event in assertions.</summary>
        public string Payload { get; init; }

        /// <inheritdoc/>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <inheritdoc/>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// A plain integration event used to exercise <see cref="Entity{TIdentifier}.AddEvent"/>.
    /// </summary>
    public sealed record TestIntegrationEvent : IIntegrationEvent
    {
        /// <summary>Creates the event with the given payload.</summary>
        public TestIntegrationEvent(string payload)
        {
            Payload = payload;
        }

        /// <summary>An arbitrary marker value used to identify this event in assertions.</summary>
        public string Payload { get; init; }

        /// <inheritdoc/>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <inheritdoc/>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// An event that implements both <see cref="IDomainEvent"/> and <see cref="IIntegrationEvent"/> -
    /// invalid by design, used to test that <see cref="Entity{TIdentifier}.AddEvent"/> rejects it.
    /// </summary>
    public sealed record DualPurposeTestEvent : IDomainEvent, IIntegrationEvent
    {
        /// <inheritdoc/>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <inheritdoc/>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
