using Azure.Core;

namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Resolves the <see cref="TokenCredential"/> used to authenticate against Azure Storage
    /// Queues. An interface - rather than calling <see cref="DefaultAzureCredentialFactory"/>
    /// directly - so a test or an unusual hosting environment can supply its own credential
    /// resolution without touching <see cref="QueueClientProxyFactory"/>.
    /// </summary>
    public interface ICredentialFactory
    {
        /// <summary>Creates (or returns a cached) credential to authenticate with.</summary>
        TokenCredential CreateCredential();
    }
}
