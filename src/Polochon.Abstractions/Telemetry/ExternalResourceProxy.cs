namespace Polochon.Abstractions.Telemetry
{
    /// <summary>
    /// A proxy class for external resources that provides telemetry tracking for method calls.
    /// </summary>
    /// <typeparam name="TResource">The type of the external resource.</typeparam>
    public class ExternalResourceProxy<TResource>
    {
        /// <summary>
        /// Gets the telemetry instance used for tracking.
        /// </summary>
        protected ITelemetry Telemetry { get; }

        private readonly string resourceType;

        private readonly string resourceName;

        /// <summary>
        /// Gets the external resource instance.
        /// </summary>
        protected TResource Resource { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalResourceProxy{TResource}"/> class.
        /// </summary>
        /// <param name="resource">The external resource instance.</param>
        /// <param name="telemetry">The telemetry instance used for tracking.</param>
        /// <param name="resourceType">The type of the resource.</param>
        /// <param name="resourceName">The name of the resource.</param>
        public ExternalResourceProxy(TResource resource, ITelemetry telemetry, string resourceType, string resourceName)
        {
            Resource = resource;
            Telemetry = telemetry;
            this.resourceType = resourceType;
            this.resourceName = resourceName;
        }

        /// <summary>
        /// Executes an asynchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the method call.</typeparam>
        /// <param name="method">The name of the method being called.</param>
        /// <param name="call">The function representing the method call.</param>
        /// <returns>A task that represents the asynchronous operation, containing the result of the method call.</returns>
        protected async Task<TResult> TelemetryCallAsync<TResult>(string method, Func<TResource, Task<TResult>> call)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool status = true;
            try
            {
                var result = await call(Resource);
                return result;
            }
            catch (Exception e)
            {
                Telemetry.TrackException(e);
                status = false;
                throw;
            }
            finally
            {
                timer.Stop();
                Telemetry.TrackDependency(resourceType, resourceName, method, startTime, timer.Elapsed, status);
            }
        }

        /// <summary>
        /// Executes an asynchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <param name="method">The name of the method being called.</param>
        /// <param name="call">The function representing the method call.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected async Task TelemetryCallAsync(string method, Func<TResource, Task> call)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool status = true;
            try
            {
                await call(Resource);
            }
            catch (Exception e)
            {
                Telemetry.TrackException(e);
                status = false;
                throw;
            }
            finally
            {
                timer.Stop();
                Telemetry.TrackDependency(resourceType, resourceName, method, startTime, timer.Elapsed, status);
            }
        }

        /// <summary>
        /// Executes a synchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <param name="method">The name of the method being called.</param>
        /// <param name="call">The action representing the method call.</param>
        protected void TelemetryCall(string method, Action<TResource> call)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool status = true;
            try
            {
                call(Resource);
            }
            catch (Exception e)
            {
                Telemetry.TrackException(e);
                status = false;
                throw;
            }
            finally
            {
                timer.Stop();
                Telemetry.TrackDependency(resourceType, resourceName, method, startTime, timer.Elapsed, status);
            }
        }

        /// <summary>
        /// Executes a synchronous method call on the resource with telemetry tracking.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the method call.</typeparam>
        /// <param name="method">The name of the method being called.</param>
        /// <param name="call">The function representing the method call.</param>
        /// <returns>The result of the method call.</returns>
        protected TResult TelemetryCall<TResult>(string method, Func<TResource, TResult> call)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool status = true;
            try
            {
                return call(Resource);
            }
            catch (Exception e)
            {
                Telemetry.TrackException(e);
                status = false;
                throw;
            }
            finally
            {
                timer.Stop();
                Telemetry.TrackDependency(resourceType, resourceName, method, startTime, timer.Elapsed, status);
            }
        }
    }
}