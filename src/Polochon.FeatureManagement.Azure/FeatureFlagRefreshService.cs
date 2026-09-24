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
        private readonly IConfigurationRefresherProvider refresherProvider;
        private readonly FeatureFlagRefreshSchedule schedule;
        private readonly TimeProvider timeProvider;
        private PeriodicTimer? timer;

        public FeatureFlagRefreshService(IConfigurationRefresherProvider refresherProvider, FeatureFlagRefreshSchedule schedule, TimeProvider timeProvider)
        {
            this.refresherProvider = refresherProvider;
            this.schedule = schedule;
            this.timeProvider = timeProvider;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Created here rather than in ExecuteAsync, which BackgroundService runs on the thread pool:
            // the refresh schedule then starts exactly when the host starts this service.
            timer = new PeriodicTimer(schedule.Interval, timeProvider);
            return base.StartAsync(cancellationToken);
        }

        public override void Dispose()
        {
            timer?.Dispose();
            base.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (await timer!.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    foreach (var refresher in refresherProvider.Refreshers)
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
