namespace Polochon.FeatureManagement.Azure
{
    /// <summary>
    /// How often <see cref="FeatureFlagRefreshService"/> asks Azure App Configuration for changes.
    /// </summary>
    internal sealed class FeatureFlagRefreshSchedule
    {
        public TimeSpan Interval { get; init; }
    }
}
