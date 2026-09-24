using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Polochon.FeatureManagement.AzureAppConfiguration.Tests
{
    /// <summary>
    /// Tests for <see cref="FeatureFlagRefreshService"/>, driven by a fake clock instead of real time.
    /// </summary>
    public sealed class FeatureFlagRefreshServiceTests
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Tests that every refresher is refreshed once per elapsed interval, and not before.
        /// </summary>
        [Fact(DisplayName = "Refresh service refreshes every refresher once per interval")]
        public async Task RefreshServiceRefreshesEveryRefresherOncePerInterval()
        {
            // Arrange
            var first = new CountingRefresher();
            var second = new CountingRefresher();
            var timeProvider = new FakeTimeProvider();
            using var service = CreateService(timeProvider, first, second);
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Act
            timeProvider.Advance(Interval - TimeSpan.FromSeconds(1));
            var beforeInterval = first.RefreshCount;
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => first.RefreshCount == 1 && second.RefreshCount == 1);
            timeProvider.Advance(Interval);
            await WaitUntilAsync(() => first.RefreshCount == 2 && second.RefreshCount == 2);

            // Assert
            Assert.Equal(0, beforeInterval);
            await service.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Tests that stopping the host stops refreshing, without surfacing the cancellation as a failure.
        /// </summary>
        [Fact(DisplayName = "Refresh service stops cleanly with the host")]
        public async Task RefreshServiceStopsCleanlyWithTheHost()
        {
            // Arrange
            var refresher = new CountingRefresher();
            var timeProvider = new FakeTimeProvider();
            using var service = CreateService(timeProvider, refresher);
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Act
            await service.StopAsync(TestContext.Current.CancellationToken);
            timeProvider.Advance(Interval);

            // Assert
            Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
            Assert.Equal(0, refresher.RefreshCount);
        }

        private static FeatureFlagRefreshService CreateService(TimeProvider timeProvider, params CountingRefresher[] refreshers)
            => new(new FakeRefresherProvider(refreshers), new FeatureFlagRefreshSchedule { Interval = Interval }, timeProvider);

        /// <summary>
        /// The refresh loop resumes on the thread pool after each fake tick, so the assertion has to wait for it.
        /// </summary>
        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!condition())
            {
                await Task.Delay(10, timeout.Token);
            }
        }
    }
}
