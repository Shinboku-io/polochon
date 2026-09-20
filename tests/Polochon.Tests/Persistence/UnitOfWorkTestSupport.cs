using Microsoft.EntityFrameworkCore;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Tests.Domain;

namespace Polochon.Tests.Persistence
{
    /// <summary>
    /// Records the payload of every <see cref="TestDomainEvent"/>/<see cref="CascadedDomainEvent"/>
    /// handled, in handling order.
    /// </summary>
    public sealed class EventRecorder
    {
        /// <summary>The payloads received so far, in handling order.</summary>
        public List<string> Received { get; } = [];
    }

    /// <summary>A handler that records every <see cref="TestDomainEvent"/> it receives.</summary>
    public sealed class RecordingDomainEventHandler : INotificationHandler<TestDomainEvent>
    {
        private readonly EventRecorder recorder;

        /// <summary>Creates the handler with the shared recorder to append to.</summary>
        public RecordingDomainEventHandler(EventRecorder recorder)
        {
            this.recorder = recorder;
        }

        /// <inheritdoc/>
        public ValueTask Handle(TestDomainEvent notification, CancellationToken cancellationToken)
        {
            recorder.Received.Add(notification.Payload);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A handler that records every <see cref="CascadedDomainEvent"/> it receives.</summary>
    public sealed class RecordingCascadedDomainEventHandler : INotificationHandler<CascadedDomainEvent>
    {
        private readonly EventRecorder recorder;

        /// <summary>Creates the handler with the shared recorder to append to.</summary>
        public RecordingCascadedDomainEventHandler(EventRecorder recorder)
        {
            this.recorder = recorder;
        }

        /// <inheritdoc/>
        public ValueTask Handle(CascadedDomainEvent notification, CancellationToken cancellationToken)
        {
            recorder.Received.Add(notification.Payload);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A handler that reacts to <see cref="TestDomainEvent"/> by raising a
    /// <see cref="CascadedDomainEvent"/> on a brand new aggregate tracked by the same context -
    /// used to prove <see cref="Polochon.Persistence.UnitOfWork{TContext}"/> keeps collecting and
    /// dispatching domain events raised by earlier handlers, instead of dropping them.
    /// </summary>
    public sealed class CascadingDomainEventHandler : INotificationHandler<TestDomainEvent>
    {
        private readonly UnitOfWorkTestDbContext context;

        /// <summary>Creates the handler with the context to track the cascaded aggregate on.</summary>
        public CascadingDomainEventHandler(UnitOfWorkTestDbContext context)
        {
            this.context = context;
        }

        /// <inheritdoc/>
        public ValueTask Handle(TestDomainEvent notification, CancellationToken cancellationToken)
        {
            var cascaded = new TestAggregate();
            cascaded.Raise(new CascadedDomainEvent("cascaded"));
            context.Aggregates.Add(cascaded);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// An <see cref="IOutbox"/> fake that just records what was published - <c>UnitOfWork</c>
    /// never drains its own outbox, so <see cref="DrainPendingMessagesAsync"/> is unused here.
    /// </summary>
    public sealed class RecordingOutbox : IOutbox
    {
        /// <summary>The messages published so far, in publish order.</summary>
        public List<IIntegrationEvent> Published { get; } = [];

        /// <inheritdoc/>
        public ValueTask PublishMessageAsync(IIntegrationEvent message, CancellationToken cancellationToken)
        {
            Published.Add(message);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<IIntegrationEvent> DrainPendingMessagesAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException("Not used by UnitOfWork.");
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> that always returns the same, pre-created
    /// context instance - used so tests can share one context between the unit of work under test
    /// and a handler that needs to track a second aggregate on it.
    /// </summary>
    public sealed class FixedDbContextFactory<TContext> : IDbContextFactory<TContext>
        where TContext : DbContext
    {
        private readonly TContext context;

        /// <summary>Creates the factory, always returning the given context instance.</summary>
        public FixedDbContextFactory(TContext context)
        {
            this.context = context;
        }

        /// <inheritdoc/>
        public TContext CreateDbContext() => context;
    }
}
