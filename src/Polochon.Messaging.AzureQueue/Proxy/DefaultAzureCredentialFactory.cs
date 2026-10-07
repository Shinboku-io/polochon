using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Default <see cref="ICredentialFactory"/>: resolves a <see cref="DefaultAzureCredential"/>
    /// that follows whatever <c>AZURE_TOKEN_CREDENTIALS</c> is already configured to - e.g.
    /// <c>prod</c> for Managed Identity once hosted in Azure, <c>dev</c> for the developer's own
    /// signed-in identity, or a single credential name. This factory does not guess or default
    /// that value; it is deployment configuration, not application logic. Logs which kind of
    /// credential was requested and, if a client id is involved (a user-assigned Managed Identity
    /// or a service principal, both configured via <c>AZURE_CLIENT_ID</c>), its first few
    /// characters - enough to tell identities apart across logs without fully exposing the id.
    /// </summary>
    public sealed class DefaultAzureCredentialFactory : ICredentialFactory
    {
        private const string ClientIdEnvironmentVariableName = "AZURE_CLIENT_ID";
        private const int ClientIdPrefixLength = 8;

        private readonly ILogger<DefaultAzureCredentialFactory> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAzureCredentialFactory"/> class.
        /// </summary>
        /// <param name="logger">Logger, resolved through the standard Microsoft.Extensions.Logging pipeline.</param>
        public DefaultAzureCredentialFactory(ILogger<DefaultAzureCredentialFactory> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public TokenCredential CreateCredential()
        {
            var selector = Environment.GetEnvironmentVariable(DefaultAzureCredential.DefaultEnvironmentVariableName);
            var clientId = Environment.GetEnvironmentVariable(ClientIdEnvironmentVariableName);

            logger.LogInformation(
                "Resolving Azure credential: {Selector} (client id: {ClientIdPrefix})",
                string.IsNullOrEmpty(selector) ? $"{DefaultAzureCredential.DefaultEnvironmentVariableName} not set - trying the full default chain" : selector,
                string.IsNullOrEmpty(clientId) ? "none" : $"{Mask(clientId)}...");

            // The environment-variable-name overload is deterministic - it throws if the variable
            // is set but unrecognized - but it also throws if the variable is simply unset, so it
            // can only be used once we know a value is actually there.
            return string.IsNullOrEmpty(selector)
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(DefaultAzureCredential.DefaultEnvironmentVariableName, new DefaultAzureCredentialOptions());
        }

        private static string Mask(string clientId)
            => clientId.Length <= ClientIdPrefixLength ? clientId : clientId[..ClientIdPrefixLength];
    }
}
