namespace Polochon.Messaging.AzureQueue.Proxy
{
    /// <summary>
    /// Defines a factory interface for creating instances of <see cref="IQueueClientProxy"/>.
    /// </summary>
    public interface IQueueClientProxyFactory
    {
        /// <summary>
        /// Gets an instance of <see cref="IQueueClientProxy"/> for the specified queue endpoint and queue name.
        /// </summary>
        /// <param name="queueEndpoint">The endpoint of the queue.</param>
        /// <param name="queueName">The name of the queue.</param>
        /// <returns>An instance of <see cref="IQueueClientProxy"/>.</returns>
        IQueueClientProxy GetProxy(string queueEndpoint, string queueName);
    }
}