using System.Collections.Concurrent;
using System.Diagnostics;

namespace Polochon.Tests.Telemetry
{
    /// <summary>
    /// Records every activity stopped by one activity source, for as long as it is not disposed.
    /// </summary>
    public sealed class ActivityCollector : IDisposable
    {
        private readonly ActivityListener listener;
        private readonly ConcurrentQueue<Activity> activities = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityCollector"/> class.
        /// </summary>
        /// <param name="sourceName">The name of the activity source to record.</param>
        public ActivityCollector(string sourceName)
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activities.Enqueue,
            };
            ActivitySource.AddActivityListener(listener);
        }

        /// <summary>
        /// Gets the activities stopped so far, in stop order.
        /// </summary>
        public IReadOnlyList<Activity> Activities => [.. activities];

        /// <inheritdoc/>
        public void Dispose() => listener.Dispose();
    }
}
