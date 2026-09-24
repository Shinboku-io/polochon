using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Polochon.Serilog.Tests
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> recording every message logged through it, standing in for any
    /// other provider (e.g. OpenTelemetry's) that should still receive events once Serilog is plugged in.
    /// </summary>
    public sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();

        /// <summary>Gets the messages logged so far.</summary>
        public IReadOnlyCollection<string> Messages => messages;

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> messages;

            public CapturingLogger(ConcurrentQueue<string> messages)
            {
                this.messages = messages;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
