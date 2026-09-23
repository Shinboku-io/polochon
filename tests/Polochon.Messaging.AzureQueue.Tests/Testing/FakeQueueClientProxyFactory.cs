using System.Collections.Concurrent;
using Polochon.Messaging.AzureQueue.Proxy;

namespace Polochon.Messaging.AzureQueue.Tests.Testing
{
    /// <summary>
    /// Hands out one <see cref="FakeQueueClientProxy"/> per distinct queue name, memoized - so a
    /// test can publish through one call to <see cref="GetProxy"/> and inspect the same in-memory
    /// queue through another, exactly like <see cref="AzureQueueInbox"/> does for its inbox and
    /// error queues.
    /// </summary>
    internal sealed class FakeQueueClientProxyFactory : IQueueClientProxyFactory
    {
        private readonly ConcurrentDictionary<string, FakeQueueClientProxy> proxies = new();

        public FakeQueueClientProxy GetProxy(string queueName) => proxies.GetOrAdd(queueName, static _ => new FakeQueueClientProxy());

        IQueueClientProxy IQueueClientProxyFactory.GetProxy(string queueEndpoint, string queueName) => GetProxy(queueName);
    }
}
