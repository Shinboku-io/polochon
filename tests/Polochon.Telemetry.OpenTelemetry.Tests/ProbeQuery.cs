using Polochon.Abstractions.CQRS;

namespace Polochon.Telemetry.OpenTelemetry.Tests
{
    /// <summary>
    /// A query handled by <see cref="FakeModule"/>, so its dispatch is traced and timed.
    /// </summary>
    public sealed record ProbeQuery : IQuery<Unit>;
}
