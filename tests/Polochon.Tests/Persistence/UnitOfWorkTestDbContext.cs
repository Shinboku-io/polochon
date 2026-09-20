using Microsoft.EntityFrameworkCore;
using Polochon.Persistence;
using Polochon.Tests.Domain;

namespace Polochon.Tests.Persistence
{
    /// <summary>
    /// Minimal EF Core context mapping <see cref="TestAggregate"/> and
    /// <see cref="DeletableTestAggregate"/>, used to exercise <see cref="UnitOfWork{TContext}"/>
    /// against a real change tracker.
    /// </summary>
    public sealed class UnitOfWorkTestDbContext : DbContext
    {
        /// <summary>Creates the context with the given options.</summary>
        public UnitOfWorkTestDbContext(DbContextOptions<UnitOfWorkTestDbContext> options)
            : base(options)
        {
        }

        /// <summary>The tracked <see cref="TestAggregate"/> instances.</summary>
        public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

        /// <summary>The tracked <see cref="DeletableTestAggregate"/> instances.</summary>
        public DbSet<DeletableTestAggregate> DeletableAggregates => Set<DeletableTestAggregate>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestAggregate>(builder =>
            {
                builder.HasKey(x => x.Identifier);
                builder.IgnoreRaisedEvents();
            });

            modelBuilder.Entity<DeletableTestAggregate>(builder =>
            {
                builder.HasKey(x => x.Identifier);
                builder.IgnoreRaisedEvents();
                builder.Ignore(x => x.OnDeleteWasCalled);
            });
        }
    }
}
