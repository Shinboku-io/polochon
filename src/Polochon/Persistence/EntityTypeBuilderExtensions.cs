using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Polochon.Abstractions.Domain;

namespace Polochon.Persistence
{
    /// <summary>
    /// EF Core model-building helpers for <see cref="Entity{TIdentifier}"/>-derived aggregate roots.
    /// </summary>
    public static class EntityTypeBuilderExtensions
    {
        /// <summary>
        /// Ignores <see cref="IDomainEventSource.DomainEvents"/> and
        /// <see cref="IIntegrationEventSource.IntegrationEvents"/>, which EF Core's model builder
        /// would otherwise try (and fail) to map as navigation properties to <see cref="IDomainEvent"/>
        /// / <see cref="IIntegrationEvent"/>. Call this for every <see cref="Entity{TIdentifier}"/>-derived
        /// aggregate root's configuration.
        /// </summary>
        public static EntityTypeBuilder<TEntity> IgnoreRaisedEvents<TEntity>(this EntityTypeBuilder<TEntity> builder)
            where TEntity : class, IDomainEventSource, IIntegrationEventSource
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Ignore(entity => entity.DomainEvents);
            builder.Ignore(entity => entity.IntegrationEvents);

            return builder;
        }
    }
}
