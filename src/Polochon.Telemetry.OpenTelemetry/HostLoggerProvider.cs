using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Polochon.Abstractions.Telemetry;

namespace Polochon.Telemetry.OpenTelemetry
{
    /// <summary>
    /// The <see cref="ILoggerProvider"/> a module's isolated container gets from <c>WithOpenTelemetry()</c>:
    /// hands the module's log entries to the host's OpenTelemetry logger provider, so every module shares
    /// the host's single pipeline (processors, exporters, resource) instead of configuring its own.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="ISupportExternalScope"/>: the host's provider already reads scopes from
    /// the host's logger factory, and handing it the module's would steal them from host logs. Scopes a
    /// module begins are pushed through the host provider's loggers instead, which works because they
    /// flow with the async context. Not disposable either: the host owns its provider.
    /// </remarks>
    internal sealed class HostLoggerProvider : ILoggerProvider
    {
        private readonly OpenTelemetryLoggerProvider hostProvider;
        private readonly IReadOnlyList<KeyValuePair<string, object?>> moduleScope;

        public HostLoggerProvider(OpenTelemetryLoggerProvider hostProvider, string moduleName)
        {
            this.hostProvider = hostProvider;
            moduleScope = [new KeyValuePair<string, object?>(PolochonTelemetry.ModuleTag, moduleName)];
        }

        public ILogger CreateLogger(string categoryName) => new ModuleLogger(hostProvider.CreateLogger(categoryName), moduleScope);

        public void Dispose()
        {
            // The host's provider is owned, and disposed, by the host.
        }
    }
}
