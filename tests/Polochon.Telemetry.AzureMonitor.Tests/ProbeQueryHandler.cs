using Polochon.Abstractions.CQRS;

namespace Polochon.Telemetry.AzureMonitor.Tests
{
    /// <summary>
    /// Handles <see cref="ProbeQuery"/>.
    /// </summary>
    public sealed class ProbeQueryHandler : IQueryHandler<ProbeQuery, Unit>
    {
        /// <inheritdoc/>
        public ValueTask<Unit> HandleAsync(ProbeQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(Unit.Value);
    }
}
