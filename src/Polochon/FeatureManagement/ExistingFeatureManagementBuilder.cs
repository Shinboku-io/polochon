using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Polochon.FeatureManagement
{
    /// <summary>
    /// An <see cref="IFeatureManagementBuilder"/> over feature management registered earlier, e.g. by
    /// <c>AddAzureAppConfigurationFeatureFlags()</c>. Microsoft.FeatureManagement's own builder is only
    /// handed out by <c>AddFeatureManagement()</c>, and calling that again either throws (when the
    /// existing registration is <c>AddScopedFeatureManagement()</c>) or registers the feature manager,
    /// its snapshot and the built-in filters a second time.
    /// </summary>
    internal sealed class ExistingFeatureManagementBuilder : IFeatureManagementBuilder
    {
        private readonly ServiceLifetime lifetime;

        public ExistingFeatureManagementBuilder(IServiceCollection services)
        {
            Services = services;

            // Filters and session managers must match the feature manager's own lifetime: singleton
            // after AddFeatureManagement(), scoped after AddScopedFeatureManagement().
            lifetime = services.First(descriptor => descriptor.ServiceType == typeof(IFeatureManager)).Lifetime;
        }

        public IServiceCollection Services { get; }

        public IFeatureManagementBuilder AddFeatureFilter<T>()
            where T : IFeatureFilterMetadata
        {
            if (!Services.Any(descriptor => descriptor.ServiceType == typeof(IFeatureFilterMetadata) && descriptor.ImplementationType == typeof(T)))
            {
                Services.Add(new ServiceDescriptor(typeof(IFeatureFilterMetadata), typeof(T), lifetime));
            }

            return this;
        }

        public IFeatureManagementBuilder AddSessionManager<T>()
            where T : ISessionManager
        {
            Services.Add(new ServiceDescriptor(typeof(ISessionManager), typeof(T), lifetime));
            return this;
        }
    }
}
