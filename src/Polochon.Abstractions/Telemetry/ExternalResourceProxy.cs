using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Polochon.Abstractions.Telemetry
{
    /// <summary>
    /// Base class for wrappers around an external resource with no telemetry of its own. Every call
    /// made through <c>TelemetryCall</c>/<c>TelemetryCallAsync</c> is traced as a
    /// <see cref="ActivityKind.Client"/> activity from the module's <see cref="IModuleTelemetry.ActivitySource"/>,
    /// and timed in the <see cref="PolochonTelemetry.DependencyDurationMetric"/> histogram.
    /// </summary>
    /// <remarks>
    /// The activity wraps the call, so anything the resource's own client emits becomes its child, and
    /// the trace context flows to the resource. A failure is recorded on the activity (error status plus
    /// the exception) and the exception is rethrown untouched - it is not reported separately, so it is
    /// not counted twice by whatever eventually catches it. Most Azure SDK clients, <c>HttpClient</c>,
    /// EF Core and SqlClient are already instrumented: wrap them only to add what they lack.
    /// </remarks>
    /// <typeparam name="TResource">The type of the external resource.</typeparam>
    public abstract class ExternalResourceProxy<TResource>
    {
        private readonly string resourceType;

        private readonly string resourceName;

        private readonly Histogram<double> duration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalResourceProxy{TResource}"/> class.
        /// </summary>
        /// <param name="resource">The external resource instance.</param>
        /// <param name="telemetry">The telemetry of the module making the calls.</param>
        /// <param name="resourceType">The kind of resource, e.g. <c>queue</c>; tagged as <see cref="PolochonTelemetry.DependencyTypeTag"/>.</param>
        /// <param name="resourceName">The resource name, e.g. the queue name; tagged as <see cref="PolochonTelemetry.DependencyNameTag"/>.</param>
        protected ExternalResourceProxy(TResource resource, IModuleTelemetry telemetry, string resourceType, string resourceName)
        {
            ArgumentNullException.ThrowIfNull(telemetry);
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

            Resource = resource;
            Telemetry = telemetry;
            this.resourceType = resourceType;
            this.resourceName = resourceName;

            // A Meter hands back its existing instrument when asked again with the same arguments, so
            // every proxy of a module shares a single histogram.
            duration = telemetry.Meter.CreateHistogram<double>(
                PolochonTelemetry.DependencyDurationMetric,
                unit: "s",
                description: "Duration of calls to external resources made through an ExternalResourceProxy.");
        }

        /// <summary>
        /// Gets the telemetry of the module making the calls.
        /// </summary>
        protected IModuleTelemetry Telemetry { get; }

        /// <summary>
        /// Gets the external resource instance.
        /// </summary>
        protected TResource Resource { get; }

        /// <summary>
        /// Executes an asynchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the method call.</typeparam>
        /// <param name="method">The name of the method being called; tagged as <see cref="PolochonTelemetry.DependencyOperationTag"/>.</param>
        /// <param name="call">The function representing the method call.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the method call.</returns>
        protected async Task<TResult> TelemetryCallAsync<TResult>(string method, Func<TResource, Task<TResult>> call)
        {
            ArgumentNullException.ThrowIfNull(call);

            var start = Stopwatch.GetTimestamp();
            using var activity = StartActivity(method);
            string? errorType = null;
            try
            {
                return await call(Resource).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errorType = RecordFailure(activity, exception);
                throw;
            }
            finally
            {
                RecordDuration(method, start, errorType);
            }
        }

        /// <summary>
        /// Executes an asynchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <param name="method">The name of the method being called; tagged as <see cref="PolochonTelemetry.DependencyOperationTag"/>.</param>
        /// <param name="call">The function representing the method call.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected async Task TelemetryCallAsync(string method, Func<TResource, Task> call)
        {
            ArgumentNullException.ThrowIfNull(call);

            var start = Stopwatch.GetTimestamp();
            using var activity = StartActivity(method);
            string? errorType = null;
            try
            {
                await call(Resource).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errorType = RecordFailure(activity, exception);
                throw;
            }
            finally
            {
                RecordDuration(method, start, errorType);
            }
        }

        /// <summary>
        /// Executes a synchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <param name="method">The name of the method being called; tagged as <see cref="PolochonTelemetry.DependencyOperationTag"/>.</param>
        /// <param name="call">The action representing the method call.</param>
        protected void TelemetryCall(string method, Action<TResource> call)
        {
            ArgumentNullException.ThrowIfNull(call);

            var start = Stopwatch.GetTimestamp();
            using var activity = StartActivity(method);
            string? errorType = null;
            try
            {
                call(Resource);
            }
            catch (Exception exception)
            {
                errorType = RecordFailure(activity, exception);
                throw;
            }
            finally
            {
                RecordDuration(method, start, errorType);
            }
        }

        /// <summary>
        /// Executes a synchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the method call.</typeparam>
        /// <param name="method">The name of the method being called; tagged as <see cref="PolochonTelemetry.DependencyOperationTag"/>.</param>
        /// <param name="call">The function representing the method call.</param>
        /// <returns>The result of the method call.</returns>
        protected TResult TelemetryCall<TResult>(string method, Func<TResource, TResult> call)
        {
            ArgumentNullException.ThrowIfNull(call);

            var start = Stopwatch.GetTimestamp();
            using var activity = StartActivity(method);
            string? errorType = null;
            try
            {
                return call(Resource);
            }
            catch (Exception exception)
            {
                errorType = RecordFailure(activity, exception);
                throw;
            }
            finally
            {
                RecordDuration(method, start, errorType);
            }
        }

        private static string RecordFailure(Activity? activity, Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            _ = activity?
                .AddException(exception)
                .SetTag(PolochonTelemetry.ErrorTypeTag, errorType)
                .SetStatus(ActivityStatusCode.Error, exception.Message);
            return errorType;
        }

        private Activity? StartActivity(string method)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(method);

            // Null when nothing listens to the module's source: tagging is then skipped entirely.
            return Telemetry.ActivitySource.StartActivity($"{resourceType} {method}", ActivityKind.Client)?
                .SetTag(PolochonTelemetry.ModuleTag, Telemetry.ModuleName)
                .SetTag(PolochonTelemetry.DependencyTypeTag, resourceType)
                .SetTag(PolochonTelemetry.DependencyNameTag, resourceName)
                .SetTag(PolochonTelemetry.DependencyOperationTag, method);
        }

        private void RecordDuration(string method, long start, string? errorType)
        {
            if (!duration.Enabled)
            {
                return;
            }

            var tags = new TagList
            {
                { PolochonTelemetry.ModuleTag, Telemetry.ModuleName },
                { PolochonTelemetry.DependencyTypeTag, resourceType },
                { PolochonTelemetry.DependencyNameTag, resourceName },
                { PolochonTelemetry.DependencyOperationTag, method },
            };
            if (errorType is not null)
            {
                tags.Add(PolochonTelemetry.ErrorTypeTag, errorType);
            }

            duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds, tags);
        }
    }
}
