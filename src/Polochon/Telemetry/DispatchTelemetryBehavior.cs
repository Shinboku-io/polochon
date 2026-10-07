using System.Diagnostics;
using System.Diagnostics.Metrics;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Results;
using Polochon.Abstractions.Telemetry;

namespace Polochon.Telemetry
{
    /// <summary>
    /// Traces and times every command and query a module handles. Registered as the module's outermost
    /// pipeline behavior, so it covers every other behavior, the validators and the handler - and sees the
    /// final <see cref="CommandResult"/> of a command, including a failure a later behavior turned an
    /// exception into.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    internal sealed class DispatchTelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        private static readonly string MessageType = typeof(TRequest).Name;

        private static readonly string MessageKind = GetMessageKind();

        private readonly IModuleTelemetry telemetry;

        private readonly Histogram<double> duration;

        public DispatchTelemetryBehavior(IModuleTelemetry telemetry)
        {
            this.telemetry = telemetry;

            // A Meter hands back its existing instrument when asked again with the same arguments.
            duration = telemetry.Meter.CreateHistogram<double>(
                PolochonTelemetry.MessageDurationMetric,
                unit: "s",
                description: "Duration of the commands and queries handled by a module.");
        }

        public async ValueTask<TResponse> HandleAsync(TRequest request, MessageHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var start = Stopwatch.GetTimestamp();

            // Null when nothing listens to the module's source: tagging is then skipped entirely.
            using var activity = telemetry.ActivitySource.StartActivity(MessageType, ActivityKind.Internal)?
                .SetTag(PolochonTelemetry.ModuleTag, telemetry.ModuleName)
                .SetTag(PolochonTelemetry.MessageTypeTag, MessageType)
                .SetTag(PolochonTelemetry.MessageKindTag, MessageKind);

            string? resultCode = null;
            string? errorType = null;
            try
            {
                var response = await next().ConfigureAwait(false);

                if (response is CommandResult commandResult)
                {
                    resultCode = commandResult.Result.Status;
                    _ = activity?.SetTag(PolochonTelemetry.ResultCodeTag, resultCode);
                    if (!commandResult.IsSuccess)
                    {
                        errorType = resultCode;
                        _ = activity?
                            .SetTag(PolochonTelemetry.ErrorTypeTag, errorType)
                            .SetStatus(ActivityStatusCode.Error, resultCode);
                    }
                }

                return response;
            }
            catch (Exception exception)
            {
                errorType = exception.GetType().FullName ?? exception.GetType().Name;
                _ = activity?
                    .AddException(exception)
                    .SetTag(PolochonTelemetry.ErrorTypeTag, errorType)
                    .SetStatus(ActivityStatusCode.Error, exception.Message);
                throw;
            }
            finally
            {
                RecordDuration(start, resultCode, errorType);
            }
        }

        private static string GetMessageKind()
            => typeof(TRequest).GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQuery<>))
                ? "query"
                : "command";

        private void RecordDuration(long start, string? resultCode, string? errorType)
        {
            if (!duration.Enabled)
            {
                return;
            }

            var tags = new TagList
            {
                { PolochonTelemetry.ModuleTag, telemetry.ModuleName },
                { PolochonTelemetry.MessageTypeTag, MessageType },
                { PolochonTelemetry.MessageKindTag, MessageKind },
            };
            if (resultCode is not null)
            {
                tags.Add(PolochonTelemetry.ResultCodeTag, resultCode);
            }

            if (errorType is not null)
            {
                tags.Add(PolochonTelemetry.ErrorTypeTag, errorType);
            }

            duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds, tags);
        }
    }
}
