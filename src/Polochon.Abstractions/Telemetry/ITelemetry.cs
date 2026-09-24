namespace Polochon.Abstractions.Telemetry
{
    /// <summary>
    /// Interface for telemetry tracking.
    /// </summary>
    public interface ITelemetry
    {
        Activity StartActivity(string activityName, ActivityKind kind = ActivityKind.Internal);

        /// <summary>
        /// Tracks an exception.
        /// </summary>
        /// <param name="e">The exception to track.</param>
        void TrackException(Exception e);

        /// <summary>
        /// Tracks a dependency.
        /// </summary>
        /// <param name="resourceType">The type of the resource.</param>
        /// <param name="resourceName">The name of the resource.</param>
        /// <param name="method">The method used.</param>
        /// <param name="startTime">The start time of the dependency call.</param>
        /// <param name="elapsed">The time elapsed during the dependency call.</param>
        /// <param name="status">The status of the dependency call.</param>
        void TrackDependency(string resourceType, string resourceName, string method, DateTime startTime, TimeSpan elapsed, bool status);

        /// <summary>
        /// Tracks an event.
        /// </summary>
        /// <param name="eventToTrack">The name of the event to track.</param>
        void TrackEvent(string eventToTrack);
    }
}