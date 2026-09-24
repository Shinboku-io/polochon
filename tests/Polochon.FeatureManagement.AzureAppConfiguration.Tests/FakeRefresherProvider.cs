using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Polochon.FeatureManagement.AzureAppConfiguration.Tests
{
    /// <summary>
    /// <see cref="IConfigurationRefresherProvider"/> handing out a fixed set of refreshers.
    /// </summary>
    internal sealed class FakeRefresherProvider : IConfigurationRefresherProvider
    {
        public FakeRefresherProvider(params IConfigurationRefresher[] refreshers)
        {
            Refreshers = refreshers;
        }

        public IEnumerable<IConfigurationRefresher> Refreshers { get; }
    }
}
