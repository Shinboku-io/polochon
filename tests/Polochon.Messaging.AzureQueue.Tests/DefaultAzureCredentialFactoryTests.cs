using Azure.Identity;
using Polochon.Messaging.AzureQueue.Proxy;
using Polochon.Messaging.AzureQueue.Tests.Testing;
using Xunit;

namespace Polochon.Messaging.AzureQueue.Tests
{
    /// <summary>
    /// Tests for <see cref="Proxy.DefaultAzureCredentialFactory"/>: it follows whatever
    /// <c>AZURE_TOKEN_CREDENTIALS</c> is already configured to - never guessing or defaulting it -
    /// and logs which kind of credential was requested plus a masked prefix of the client id, when
    /// one is configured.
    /// </summary>
    public sealed class DefaultAzureCredentialFactoryTests : IDisposable
    {
        private const string TokenCredentialsVariable = "AZURE_TOKEN_CREDENTIALS";
        private const string ClientIdVariable = "AZURE_CLIENT_ID";

        private readonly string? originalTokenCredentials = Environment.GetEnvironmentVariable(TokenCredentialsVariable);
        private readonly string? originalClientId = Environment.GetEnvironmentVariable(ClientIdVariable);

        /// <summary>Restores both environment variables to whatever they were before this test ran.</summary>
        public void Dispose()
        {
            Environment.SetEnvironmentVariable(TokenCredentialsVariable, originalTokenCredentials);
            Environment.SetEnvironmentVariable(ClientIdVariable, originalClientId);
        }

        /// <summary>With no <c>AZURE_TOKEN_CREDENTIALS</c> configured, the full default chain is used - not thrown, not guessed.</summary>
        [Fact]
        public void CreateCredential_WithNoEnvironmentVariable_ReturnsDefaultChain_WithoutThrowing()
        {
            Environment.SetEnvironmentVariable(TokenCredentialsVariable, null);
            var factory = new DefaultAzureCredentialFactory(new CapturingLogger<DefaultAzureCredentialFactory>());

            var credential = factory.CreateCredential();

            Assert.IsType<DefaultAzureCredential>(credential);
        }

        /// <summary>A valid <c>AZURE_TOKEN_CREDENTIALS</c> value (e.g. "dev") is followed as-is.</summary>
        [Fact]
        public void CreateCredential_WithValidEnvironmentVariable_ReturnsCredential()
        {
            Environment.SetEnvironmentVariable(TokenCredentialsVariable, "dev");
            var factory = new DefaultAzureCredentialFactory(new CapturingLogger<DefaultAzureCredentialFactory>());

            var credential = factory.CreateCredential();

            Assert.IsType<DefaultAzureCredential>(credential);
        }

        /// <summary>
        /// An invalid <c>AZURE_TOKEN_CREDENTIALS</c> value is not swallowed - the factory does not
        /// second-guess misconfiguration, it surfaces it.
        /// </summary>
        [Fact]
        public void CreateCredential_WithInvalidEnvironmentVariable_Throws()
        {
            Environment.SetEnvironmentVariable(TokenCredentialsVariable, "not-a-real-credential");
            var factory = new DefaultAzureCredentialFactory(new CapturingLogger<DefaultAzureCredentialFactory>());

            Assert.Throws<InvalidOperationException>(() => factory.CreateCredential());
        }

        /// <summary>The configured selector is logged verbatim, so it is obvious which kind of credential was requested.</summary>
        [Fact]
        public void CreateCredential_LogsTheConfiguredSelector()
        {
            Environment.SetEnvironmentVariable(TokenCredentialsVariable, "prod");
            Environment.SetEnvironmentVariable(ClientIdVariable, null);
            var logger = new CapturingLogger<DefaultAzureCredentialFactory>();
            var factory = new DefaultAzureCredentialFactory(logger);

            _ = factory.CreateCredential();

            Assert.Contains(logger.Messages, m => m.Contains("prod", StringComparison.Ordinal));
        }

        /// <summary>
        /// When a client id is configured, only its first few characters are logged - enough to
        /// tell identities apart without exposing the full id.
        /// </summary>
        [Fact]
        public void CreateCredential_WithClientId_LogsOnlyAPrefix_NotTheFullId()
        {
            Environment.SetEnvironmentVariable(TokenCredentialsVariable, "prod");
            Environment.SetEnvironmentVariable(ClientIdVariable, "11111111-2222-3333-4444-555555555555");
            var logger = new CapturingLogger<DefaultAzureCredentialFactory>();
            var factory = new DefaultAzureCredentialFactory(logger);

            _ = factory.CreateCredential();

            var message = Assert.Single(logger.Messages);
            Assert.Contains("11111111", message, StringComparison.Ordinal);
            Assert.DoesNotContain("11111111-2222-3333-4444-555555555555", message, StringComparison.Ordinal);
        }
    }
}
