using Microsoft.EntityFrameworkCore;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Persistence;

namespace Polochon.Persistence
{
    /// <summary>
    /// EF Core backed <see cref="IUnitOfWork"/>. Owns a dedicated <typeparamref name="TContext"/>
    /// instance for its entire lifetime, created via <see cref="IDbContextFactory{TContext}"/>
    /// rather than resolved from the ambient DI scope. This keeps one short-lived context per unit
    /// of work instead of a single context shared for the lifetime of a DI scope that may outlive
    /// it (e.g. a Blazor Server circuit). Repositories participating in the same unit of work must
    /// be resolved against <see cref="Context"/>, not against an independently created context -
    /// but see <see cref="RollbackAsync"/>, which replaces that instance.
    /// </summary>
    /// <typeparam name="TContext">The concrete EF Core context type.</typeparam>
    public class UnitOfWork<TContext> : IUnitOfWork, IDisposable, IAsyncDisposable
        where TContext : DbContext
    {
        private readonly IDbContextFactory<TContext> contextFactory;
        private readonly IOutboxWriter outbox;
        private readonly INotificationPublisher notificationPublisher;
        private readonly List<IIntegrationEvent> pendingIntegrationEvents = [];
        private bool disposed;

        /// <summary>
        /// Creates a unit of work with its own <typeparamref name="TContext"/> instance, created
        /// via <paramref name="contextFactory"/>.
        /// </summary>
        /// <param name="contextFactory">Creates the owned context - once now, and again by <see cref="RollbackAsync"/>.</param>
        /// <param name="outbox">Where committed integration events are published.</param>
        /// <param name="notificationPublisher">Where domain events are dispatched.</param>
        public UnitOfWork(
            IDbContextFactory<TContext> contextFactory,
            IOutboxWriter outbox,
            INotificationPublisher notificationPublisher)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            this.contextFactory = contextFactory;
            Context = contextFactory.CreateDbContext();
            this.outbox = outbox;
            this.notificationPublisher = notificationPublisher;
        }

        /// <summary>
        /// The context instance currently owned by this unit of work. Register repositories to
        /// resolve this same instance within the same DI scope, so they share it with
        /// <see cref="CommitAsync"/> - but re-resolve it after calling <see cref="RollbackAsync"/>,
        /// which replaces it with a new instance.
        /// </summary>
        public TContext Context { get; private set; }

        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await PreSaveChangesAsync(cancellationToken);

            _ = await Context.SaveChangesAsync(cancellationToken);

            await PostSaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Discards every change tracked so far, without ever touching the database - only
        /// meaningful before <see cref="CommitAsync"/>, since EF Core's implicit-transaction
        /// <c>SaveChanges</c> gives no way to undo a commit that already succeeded.
        /// </summary>
        /// <remarks>
        /// Clearing the change tracker (<c>ChangeTracker.Clear()</c>) is not enough on its own: it
        /// stops tracking modified entities without restoring the property values application code
        /// already mutated in memory, which would leave those objects silently wrong even though
        /// they're no longer tracked. EF Core also has no general, reliable way to revert an
        /// arbitrary tracked graph (relationship/navigation changes in particular) one entity at a
        /// time. The only way to guarantee a true reset is to discard the context outright: this
        /// disposes the current <see cref="Context"/> and replaces it with a freshly created one
        /// from the same <see cref="IDbContextFactory{TContext}"/>.
        /// Anything obtained through the old context - including entities returned by repositories
        /// earlier in this unit of work - is invalid afterwards. A repository that resolved
        /// <see cref="Context"/> before this call must not be used again; re-resolve it (or the
        /// repository) after rolling back.
        /// </remarks>
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            var previousContext = Context;

            Context = await contextFactory.CreateDbContextAsync(cancellationToken);

            await previousContext.DisposeAsync();
        }

        /// <summary>
        /// Runs before <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>: processes
        /// pending deletes, dispatches domain events, and collects integration events for
        /// publishing after the commit succeeds. Override to add steps to this phase.
        /// </summary>
        protected virtual async Task PreSaveChangesAsync(CancellationToken token = default)
        {
            // Let entities being deleted raise their events before the change tracker forgets them.
            DoOnDeleteProcessing();

            // Dispatched synchronously, in-process, before the changes are saved: domain event
            // handlers are part of the same unit of work and can still influence what gets
            // persisted (e.g. by mutating another aggregate).
            await DispatchDomainEventsAsync(token);

            // Collected last so integration events raised by cascading domain event handlers
            // above are captured too. Not published yet - only once the transaction commits.
            CollectIntegrationEvents();
        }

        /// <summary>
        /// Runs after <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> has succeeded:
        /// publishes the integration events collected during <see cref="PreSaveChangesAsync"/>.
        /// Override to add steps to this phase.
        /// </summary>
        protected virtual async Task PostSaveChangesAsync(CancellationToken token = default)
        {
            // Only reached once SaveChanges has succeeded: integration events must never be
            // handed to the outbox for a transaction that failed to commit.
            await PublishIntegrationEventsAsync(token);
        }

        private async Task DispatchDomainEventsAsync(CancellationToken token)
        {
            // Looping instead of a single collect-then-dispatch pass: a handler reacting to one
            // domain event may raise new ones (directly, or by mutating another aggregate). A
            // single pass would silently drop those. Keep collecting and dispatching until an
            // iteration raises nothing new.
            while (true)
            {
                var batch = CollectDomainEvents();
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var domainEvent in batch)
                {
                    await notificationPublisher.PublishAsync(domainEvent, token);
                }
            }
        }

        private List<IDomainEvent> CollectDomainEvents()
        {
            var domainEntities = Context.ChangeTracker
               .Entries<IDomainEventSource>()
               .Where(x => x.Entity.DomainEvents.Count > 0)
               .ToList();

            var events = domainEntities.SelectMany(x => x.Entity.DomainEvents).ToList();
            domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());

            return events;
        }

        private void CollectIntegrationEvents()
        {
            var integrationEntities = Context.ChangeTracker
               .Entries<IIntegrationEventSource>()
               .Where(x => x.Entity.IntegrationEvents.Count > 0)
               .ToList();

            pendingIntegrationEvents.AddRange(integrationEntities
                .SelectMany(x => x.Entity.IntegrationEvents));

            integrationEntities.ForEach(entity => entity.Entity.ClearIntegrationEvents());
        }

        private async Task PublishIntegrationEventsAsync(CancellationToken token)
        {
            foreach (var integrationEvent in pendingIntegrationEvents)
            {
                await outbox.PublishMessageAsync(integrationEvent, token);
            }

            pendingIntegrationEvents.Clear();
        }

        private void DoOnDeleteProcessing()
        {
            var deletedEntries = Context.ChangeTracker
                .Entries<IRaiseEventOnDelete>()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in deletedEntries)
            {
                entry.Entity.OnDelete();
            }
        }

        /// <summary>
        /// Disposes the owned <see cref="Context"/> when <paramref name="disposing"/> is <c>true</c>.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Context.Dispose();
                }

                disposed = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                await Context.DisposeAsync();
                disposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
