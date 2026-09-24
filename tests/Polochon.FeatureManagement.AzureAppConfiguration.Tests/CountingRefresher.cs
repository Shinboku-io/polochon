using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Polochon.FeatureManagement.AzureAppConfiguration.Tests
{
    /// <summary>
    /// <see cref="IConfigurationRefresher"/> counting the refreshes it is asked for, without any store.
    /// </summary>
    internal sealed class CountingRefresher : IConfigurationRefresher
    {
        private int _refreshCount;

        public int RefreshCount => Volatile.Read(ref _refreshCount);

        public Uri AppConfigurationEndpoint { get; } = new("https://polochon-test.invalid");

        public Task RefreshAsync(CancellationToken cancellationToken = default)
            => TryRefreshAsync(cancellationToken);

        public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _refreshCount);
            return Task.FromResult(true);
        }

        public void ProcessPushNotification(PushNotification pushNotification, TimeSpan? maxDelay = null)
        {
        }
    }
}
