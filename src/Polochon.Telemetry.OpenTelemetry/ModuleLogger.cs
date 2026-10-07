using Microsoft.Extensions.Logging;

namespace Polochon.Telemetry.OpenTelemetry
{
    /// <summary>
    /// Wraps a host OpenTelemetry logger so every entry a module logs carries the module's name
    /// (<c>polochon.module</c>) - exported as a log attribute, since the host includes scopes.
    /// </summary>
    internal sealed class ModuleLogger : ILogger
    {
        private readonly ILogger hostLogger;
        private readonly IReadOnlyList<KeyValuePair<string, object?>> moduleScope;

        public ModuleLogger(ILogger hostLogger, IReadOnlyList<KeyValuePair<string, object?>> moduleScope)
        {
            this.hostLogger = hostLogger;
            this.moduleScope = moduleScope;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => hostLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => hostLogger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!hostLogger.IsEnabled(logLevel))
            {
                return;
            }

            using var scope = hostLogger.BeginScope(moduleScope);
            hostLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
