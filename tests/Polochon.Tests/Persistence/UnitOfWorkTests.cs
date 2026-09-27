using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Mediation;
using Polochon.Persistence;
using Polochon.Tests.Domain;
using Xunit;

namespace Polochon.Tests.Persistence
{
    /// <summary>
    /// Tests for <see cref="UnitOfWork{TContext}"/>: domain event dispatch (including cascades),
    /// integration event publishing, delete hooks, rollback, and disposal.
    /// </summary>
    public sealed class UnitOfWorkTests
    {
        private static UnitOfWorkTestDbContext CreateContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<UnitOfWorkTestDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
            return new UnitOfWorkTestDbContext(options);
        }

        // A real IDbContextFactory (not FixedDbContextFactory), because RollbackAsync calls
        // CreateDbContextAsync again and needs a genuinely new instance each time - unlike the
        // other tests here, which only ever need the one context created up front.
        private static IDbContextFactory<UnitOfWorkTestDbContext> CreateRealContextFactory(string databaseName)
        {
            var services = new ServiceCollection();
            _ = services.AddDbContextFactory<UnitOfWorkTestDbContext>(options => options.UseInMemoryDatabase(databaseName));
            return services.BuildServiceProvider().GetRequiredService<IDbContextFactory<UnitOfWorkTestDbContext>>();
        }

        /// <summary>A domain event raised on a tracked aggregate is dispatched to its handler on commit.</summary>
        [Fact]
        public async Task CommitAsync_DispatchesRaisedDomainEvents_ToRegisteredHandlers()
        {
            var recorder = new EventRecorder();
            var services = new ServiceCollection();
            services.AddSingleton(recorder);
            services.AddDispatcher(typeof(RecordingDomainEventHandler));
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var context = CreateContext(nameof(CommitAsync_DispatchesRaisedDomainEvents_ToRegisteredHandlers));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), new RecordingOutbox(), publisher);

            var aggregate = new TestAggregate();
            aggregate.Raise(new TestDomainEvent("hello"));
            unitOfWork.Context.Aggregates.Add(aggregate);

            await unitOfWork.CommitAsync();

            Assert.Equal(["hello"], recorder.Received);
        }

        /// <summary>
        /// Regression test: a single collect-then-dispatch pass would silently drop an event
        /// raised by a handler reacting to an earlier one. UnitOfWork must keep looping.
        /// </summary>
        [Fact]
        public async Task CommitAsync_DomainEventRaisedByAHandler_IsAlsoDispatched()
        {
            var recorder = new EventRecorder();
            var context = CreateContext(nameof(CommitAsync_DomainEventRaisedByAHandler_IsAlsoDispatched));

            var services = new ServiceCollection();
            services.AddSingleton(recorder);
            services.AddSingleton(context);
            services.AddDispatcher(
                typeof(RecordingDomainEventHandler),
                typeof(CascadingDomainEventHandler),
                typeof(RecordingCascadedDomainEventHandler));
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), new RecordingOutbox(), publisher);

            var aggregate = new TestAggregate();
            aggregate.Raise(new TestDomainEvent("trigger"));
            unitOfWork.Context.Aggregates.Add(aggregate);

            await unitOfWork.CommitAsync();

            Assert.Equal(["trigger", "cascaded"], recorder.Received);
        }

        /// <summary>An integration event is only handed to the outbox once SaveChanges has run.</summary>
        [Fact]
        public async Task CommitAsync_PublishesRaisedIntegrationEvents_ToTheOutbox_OnlyAfterSaveChangesSucceeds()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();
            var outbox = new RecordingOutbox();

            var context = CreateContext(nameof(CommitAsync_PublishesRaisedIntegrationEvents_ToTheOutbox_OnlyAfterSaveChangesSucceeds));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), outbox, publisher);

            var aggregate = new TestAggregate();
            var integrationEvent = new TestIntegrationEvent("payload");
            aggregate.Raise(integrationEvent);
            unitOfWork.Context.Aggregates.Add(aggregate);

            Assert.Empty(outbox.Published);

            await unitOfWork.CommitAsync();

            Assert.Same(integrationEvent, Assert.Single(outbox.Published));
        }

        /// <summary>Both event collections are cleared after a successful commit.</summary>
        [Fact]
        public async Task CommitAsync_ClearsRaisedEvents_AfterDispatchingAndPublishingThem()
        {
            var services = new ServiceCollection();
            services.AddSingleton<EventRecorder>();
            services.AddDispatcher(typeof(RecordingDomainEventHandler));
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var context = CreateContext(nameof(CommitAsync_ClearsRaisedEvents_AfterDispatchingAndPublishingThem));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), new RecordingOutbox(), publisher);

            var aggregate = new TestAggregate();
            aggregate.Raise(new TestDomainEvent("hello"));
            aggregate.Raise(new TestIntegrationEvent("payload"));
            unitOfWork.Context.Aggregates.Add(aggregate);

            await unitOfWork.CommitAsync();

            Assert.Empty(aggregate.DomainEvents);
            Assert.Empty(aggregate.IntegrationEvents);
        }

        /// <summary>Committing actually calls SaveChanges, moving the added entity out of the Added state.</summary>
        [Fact]
        public async Task CommitAsync_PersistsTheAddedAggregate()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var context = CreateContext(nameof(CommitAsync_PersistsTheAddedAggregate));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), new RecordingOutbox(), publisher);

            var aggregate = new TestAggregate();
            unitOfWork.Context.Aggregates.Add(aggregate);

            await unitOfWork.CommitAsync();

            Assert.Equal(EntityState.Unchanged, unitOfWork.Context.Entry(aggregate).State);
        }

        /// <summary>
        /// Rollback replaces <c>Context</c> with a brand new instance - clearing the change
        /// tracker on the same instance would not undo in-memory property changes application code
        /// already made, so a true reset needs a fresh context.
        /// </summary>
        [Fact]
        public async Task RollbackAsync_ReplacesContext_WithANewInstance()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var contextFactory = CreateRealContextFactory(nameof(RollbackAsync_ReplacesContext_WithANewInstance));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(contextFactory, new RecordingOutbox(), publisher);
            var contextBeforeRollback = unitOfWork.Context;

            await unitOfWork.RollbackAsync();

            Assert.NotSame(contextBeforeRollback, unitOfWork.Context);
            Assert.Empty(unitOfWork.Context.ChangeTracker.Entries());
        }

        /// <summary>The context replaced by a rollback is disposed, not just abandoned.</summary>
        [Fact]
        public async Task RollbackAsync_DisposesThePreviousContext()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var contextFactory = CreateRealContextFactory(nameof(RollbackAsync_DisposesThePreviousContext));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(contextFactory, new RecordingOutbox(), publisher);
            var contextBeforeRollback = unitOfWork.Context;

            await unitOfWork.RollbackAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => contextBeforeRollback.Aggregates.ToListAsync());
        }

        /// <summary>
        /// An aggregate added but never committed is gone after a rollback - verified against the
        /// underlying store through the fresh context, not just against the change tracker.
        /// </summary>
        [Fact]
        public async Task RollbackAsync_DiscardsPendingAdditions_SoTheyAreNeverPersisted()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var contextFactory = CreateRealContextFactory(nameof(RollbackAsync_DiscardsPendingAdditions_SoTheyAreNeverPersisted));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(contextFactory, new RecordingOutbox(), publisher);

            unitOfWork.Context.Aggregates.Add(new TestAggregate());

            await unitOfWork.RollbackAsync();

            Assert.Equal(0, await unitOfWork.Context.Aggregates.CountAsync());
        }

        /// <summary>The unit of work stays usable after a rollback: a later commit still persists.</summary>
        [Fact]
        public async Task RollbackAsync_LeavesTheUnitOfWorkUsable_ForFurtherWork()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var contextFactory = CreateRealContextFactory(nameof(RollbackAsync_LeavesTheUnitOfWorkUsable_ForFurtherWork));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(contextFactory, new RecordingOutbox(), publisher);

            unitOfWork.Context.Aggregates.Add(new TestAggregate());
            await unitOfWork.RollbackAsync();

            unitOfWork.Context.Aggregates.Add(new TestAggregate());
            await unitOfWork.CommitAsync();

            Assert.Equal(1, await unitOfWork.Context.Aggregates.CountAsync());
        }

        /// <summary>A deleted entity's <c>OnDelete</c> hook runs, and its event is dispatched normally.</summary>
        [Fact]
        public async Task CommitAsync_OnDeletedEntity_CallsOnDelete_AndDispatchesItsEvent()
        {
            var recorder = new EventRecorder();
            var services = new ServiceCollection();
            services.AddSingleton(recorder);
            services.AddDispatcher(typeof(RecordingDomainEventHandler));
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var context = CreateContext(nameof(CommitAsync_OnDeletedEntity_CallsOnDelete_AndDispatchesItsEvent));
            await using var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), new RecordingOutbox(), publisher);

            var entity = new DeletableTestAggregate();
            unitOfWork.Context.DeletableAggregates.Add(entity);
            await unitOfWork.CommitAsync();

            unitOfWork.Context.DeletableAggregates.Remove(entity);

            await unitOfWork.CommitAsync();

            Assert.True(entity.OnDeleteWasCalled);
            Assert.Equal(["deleted"], recorder.Received);
        }

        /// <summary>Disposing the unit of work disposes the context it owns.</summary>
        [Fact]
        public async Task DisposeAsync_DisposesTheOwnedContext()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            var context = CreateContext(nameof(DisposeAsync_DisposesTheOwnedContext));
            var unitOfWork = new UnitOfWork<UnitOfWorkTestDbContext>(
                new FixedDbContextFactory<UnitOfWorkTestDbContext>(context), new RecordingOutbox(), publisher);

            await unitOfWork.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => context.Aggregates.ToListAsync());
        }
    }
}
