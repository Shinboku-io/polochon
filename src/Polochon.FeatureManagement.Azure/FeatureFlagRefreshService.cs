using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;

namespace Polochon.FeatureManagement.Azure
{
    /// <summary>
    /// Periodically refreshes the host's Azure App Configuration data, so feature flag changes reach
    /// the host - and through it every module - while the host runs. A background service rather than
    /// the provider's ASP.NET Core middleware: it keeps the package host-agnostic (worker services have
    /// no requests), and a Blazor Server circuit barely goes through the request pipeline anyway.
    /// </summary>
    internal sealed class FeatureFlagRefreshService : BackgroundService
    {
        private readonly IConfigurationRefresherProvider _refresherProvider;
        private readonly FeatureFlagRefreshSchedule _schedule;
        private readonly TimeProvider _timeProvider;
        private PeriodicTimer? _timer;

        public FeatureFlagRefreshService(IConfigurationRefresherProvider refresherProvider, FeatureFlagRefreshSchedule schedule, TimeProvider timeProvider)
        {
            _refresherProvider = refresherProvider;
            _schedule = schedule;
            _timeProvider = timeProvider;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Created here rather than in ExecuteAsync, which BackgroundService runs on the thread pool:
            // the refresh schedule then starts exactly when the host starts this service.
            _timer = new PeriodicTimer(_schedule.Interval, _timeProvider);
            return base.StartAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (await _timer!.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    foreach (var refresher in _refresherProvider.Refreshers)
                    {
                        // TryRefreshAsync, not RefreshAsync: a failed refresh (store unreachable,
                        // throttled...) is logged by the provider and keeps the last known flags, rather
                        // than stopping this service - the next tick simply tries again.
                        _ = await refresher.TryRefreshAsync(stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutting down.
            }
        }
    }
}
