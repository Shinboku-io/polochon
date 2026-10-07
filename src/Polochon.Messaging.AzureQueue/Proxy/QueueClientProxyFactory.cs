using Azure.Core;
using Azure.Storage.Queues;

namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Default <see cref="IQueueClientProxyFactory"/>: builds a <see cref="QueueClientProxy"/>
    /// around a real Azure <see cref="QueueClient"/>, authenticated for the current environment.
    /// </summary>
    public class QueueClientProxyFactory : IQueueClientProxyFactory
    {
        private readonly Lazy<TokenCredential> credential;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClientProxyFactory"/> class.
        /// </summary>
        /// <param name="credentialFactory">Resolves the credential used to authenticate every <see cref="QueueClient"/> this factory creates.</param>
        public QueueClientProxyFactory(ICredentialFactory credentialFactory)
        {
            // Resolved once and reused for every queue this factory ever proxies - Azure's own
            // guidance is to reuse credential instances where possible, since each one caches the
            // tokens it acquires.
            credential = new Lazy<TokenCredential>(credentialFactory.CreateCredential);
        }

        /// <summary>
        /// Gets a proxy for the specified queue.
        /// </summary>
        /// <param name="queueEndpoint">The endpoint of the queue.</param>
        /// <param name="queueName">The name of the queue.</param>
        /// <returns>An instance of <see cref="IQueueClientProxy"/>.</returns>
        public virtual IQueueClientProxy GetProxy(string queueEndpoint, string queueName)
        {
            QueueClient rawClient = ConfigureQueueClient(queueEndpoint, queueName);

            var client = new QueueClientProxy(rawClient);
            return client;
        }

        /// <summary>
        /// Configures the <see cref="QueueClient"/> for the specified queue.
        /// </summary>
        /// <param name="queueEndpoint">The endpoint of the queue.</param>
        /// <param name="queueName">The name of the queue.</param>
        /// <returns>An instance of <see cref="QueueClient"/>.</returns>
        private QueueClient ConfigureQueueClient(string queueEndpoint, string queueName)
        {
            QueueClientOptions options = new(QueueClientOptions.ServiceVersion.V2025_07_05)
            {
                MessageEncoding = QueueMessageEncoding.None,
            };

            QueueClient rawClient;
            if (queueEndpoint == "UseDevelopmentStorage=true" || queueEndpoint.Contains("AccountName=devstoreaccount1"))
            {
                rawClient = new QueueClient(queueEndpoint, queueName, options);
            }
            else
            {
                var rootUri = new Uri(queueEndpoint);
                var queueUri = new Uri(rootUri, queueName);
                rawClient = new QueueClient(queueUri, credential.Value, options);
            }

            return rawClient;
        }
    }
}
