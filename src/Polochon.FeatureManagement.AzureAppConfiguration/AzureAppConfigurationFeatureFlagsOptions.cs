using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Polochon.FeatureManagement.AzureAppConfiguration
{
    /// <summary>
    /// Connects the host's feature definitions to an Azure App Configuration store. Everything here -
    /// endpoint, credential, labels, Key Vault access - is host plumbing: modules only ever see the
    /// resulting feature definitions, through <c>WithFeatureManagement()</c>.
    /// </summary>
    public sealed class AzureAppConfigurationFeatureFlagsOptions
    {
        /// <summary>
        /// The store endpoint, e.g. <c>https://{store}.azconfig.io</c>, authenticated with
        /// <see cref="Credential"/>. The recommended way to connect once hosted in Azure (Managed
        /// Identity). Set either this or <see cref="ConnectionString"/>, not both.
        /// </summary>
        public Uri? Endpoint { get; set; }

        /// <summary>
        /// The store connection string. Embeds an access key, so keep it out of source control (user
        /// secrets, environment variables...); prefer <see cref="Endpoint"/> outside local development.
        /// Set either this or <see cref="Endpoint"/>, not both.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// The credential used to reach the store (with <see cref="Endpoint"/>) and any Key Vault
        /// referenced by loaded key-values. Defaults to a <see cref="DefaultAzureCredential"/>
        /// following <c>AZURE_TOKEN_CREDENTIALS</c> when set (e.g. <c>prod</c> for Managed Identity,
        /// <c>dev</c> for the developer's own signed-in identity), or the full default chain otherwise.
        /// </summary>
        public TokenCredential? Credential { get; set; }

        /// <summary>
        /// An optional label, typically the environment name (e.g. <c>Production</c>). Feature flags
        /// with no label are loaded first, then flags with this label override them - so a flag only
        /// needs a labelled copy where an environment differs from the default.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// How often feature flags are refreshed from the store while the host runs. Defaults to
        /// 30 seconds; must be at least 1 second.
        /// </summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether the host starts even when the store cannot be reached at startup - in which case
        /// every flag reads as disabled (or falls back to definitions from other configuration sources,
        /// such as appsettings) until a refresh succeeds. Defaults to <see langword="false"/>: startup fails.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Whether plain key-values are loaded too, not only feature flags. Defaults to
        /// <see langword="false"/>: a feature flags package does not silently import the store's
        /// settings into the host's configuration. When <see langword="true"/>, every key-value with no
        /// label is loaded unless <see cref="ConfigureProvider"/> selects others.
        /// </summary>
        public bool IncludeKeyValues { get; set; }

        /// <summary>
        /// An escape hatch for anything else the Azure App Configuration provider supports (key-value
        /// selection and refresh, replicas, startup timeout...), applied after Polochon's own settings.
        /// </summary>
        public Action<AzureAppConfigurationOptions>? ConfigureProvider { get; set; }
    }
}
