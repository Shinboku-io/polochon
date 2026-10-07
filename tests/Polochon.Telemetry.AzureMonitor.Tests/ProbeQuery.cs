using Polochon.Abstractions.CQRS;

namespace Polochon.Telemetry.AzureMonitor.Tests
{
    /// <summary>
    /// A query handled by <see cref="FakeModule"/>, so its dispatch is traced.
    /// </summary>
    public sealed record ProbeQuery : IQuery<Unit>;
}
