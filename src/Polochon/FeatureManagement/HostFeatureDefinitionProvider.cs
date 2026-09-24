using Microsoft.FeatureManagement;

namespace Polochon.FeatureManagement
{
    /// <summary>
    /// The <see cref="IFeatureDefinitionProvider"/> a module's isolated container sees: forwards every
    /// lookup to the host's own provider, so feature definitions have a single source of truth owned by
    /// the host (appsettings, Azure App Configuration, ...) and a module never needs that source's
    /// configuration, endpoint or credentials. Only definitions cross the boundary - feature filters,
    /// targeting and per-scope snapshots are still evaluated inside the module.
    /// </summary>
    /// <remarks>
    /// A dedicated forwarder rather than the host's instance registered as-is: the module container
    /// never owns (and so never disposes) the host's provider, and this is the seam to narrow what a
    /// module sees later on (e.g. only its own features) without changing any module.
    /// </remarks>
    internal sealed class HostFeatureDefinitionProvider : IFeatureDefinitionProvider
    {
        private readonly IFeatureDefinitionProvider _hostProvider;

        public HostFeatureDefinitionProvider(IFeatureDefinitionProvider hostProvider)
        {
            _hostProvider = hostProvider;
        }

        /// <inheritdoc/>
        public Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
            => _hostProvider.GetFeatureDefinitionAsync(featureName);

        /// <inheritdoc/>
        public IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
            => _hostProvider.GetAllFeatureDefinitionsAsync();
    }
}
