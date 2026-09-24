using Microsoft.FeatureManagement;

namespace Polochon.FeatureManagement
{
    /// <summary>
    /// The <see cref="IFeatureDefinitionProvider"/> a module's isolated container sees: a view of the
    /// host's own provider restricted to the module's flags. Feature definitions keep a single source of
    /// truth owned by the host (appsettings, Azure App Configuration, ...), and a module never needs that
    /// source's configuration, endpoint or credentials. Only definitions cross the boundary - feature
    /// filters, targeting and per-scope snapshots are still evaluated inside the module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A module's flags are the host's flags named <c>{module name}.{flag}</c>. The module uses the short
    /// name: <c>IsEnabledAsync("BulkImport")</c> in the <c>inventory</c> module reads the host's
    /// <c>inventory.BulkImport</c> definition. Flags of other modules, and flags with no module prefix,
    /// are invisible to it, so modules can neither read nor collide on each other's flags. The prefix is
    /// matched ignoring case, like feature names themselves.
    /// </para>
    /// <para>
    /// Definitions are copied, never renamed in place: the host's provider caches the instances it hands
    /// out, and the host keeps evaluating them under their full name. The module container does not own
    /// (and so never disposes) the host's provider.
    /// </para>
    /// </remarks>
    internal sealed class HostFeatureDefinitionProvider : IFeatureDefinitionProvider
    {
        private readonly IFeatureDefinitionProvider hostProvider;
        private readonly string prefix;

        public HostFeatureDefinitionProvider(IFeatureDefinitionProvider hostProvider, string moduleName)
        {
            this.hostProvider = hostProvider;
            prefix = GetFlagPrefix(moduleName);
        }

        /// <summary>
        /// Gets the prefix the host's flags carry for the given module: <c>{moduleName}.</c>.
        /// </summary>
        public static string GetFlagPrefix(string moduleName) => moduleName + ".";

        /// <inheritdoc/>
        public async Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
        {
            var definition = await hostProvider.GetFeatureDefinitionAsync(prefix + featureName).ConfigureAwait(false);

            // Null when the host has no such flag: the feature manager then treats it as disabled.
            return definition is null ? null! : WithName(definition, featureName);
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
        {
            await foreach (var definition in hostProvider.GetAllFeatureDefinitionsAsync().ConfigureAwait(false))
            {
                if (definition.Name.Length > prefix.Length && definition.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return WithName(definition, definition.Name[prefix.Length..]);
                }
            }
        }

        private static FeatureDefinition WithName(FeatureDefinition definition, string name) => new()
        {
            Name = name,
            EnabledFor = definition.EnabledFor,
            RequirementType = definition.RequirementType,
            Status = definition.Status,
            Allocation = definition.Allocation,
            Variants = definition.Variants,
            Telemetry = definition.Telemetry,
        };
    }
}
