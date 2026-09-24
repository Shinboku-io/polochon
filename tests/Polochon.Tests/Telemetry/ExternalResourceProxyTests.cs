using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Polochon.Abstractions.Telemetry;
using Xunit;

namespace Polochon.Tests.Telemetry
{
    /// <summary>
    /// Tests for <see cref="ExternalResourceProxy{TResource}"/>: calls to an external resource are traced
    /// as client activities of the calling module and timed in the dependency duration histogram.
    /// </summary>
    public sealed class ExternalResourceProxyTests : IAsyncLifetime
    {
        private const string ModuleName = "telemetry-proxy-tests";

        private readonly TelemetryModule module = new(ModuleName);
        private readonly ActivityCollector activities = new(PolochonTelemetry.GetModuleSourceName(ModuleName));

        /// <summary>Initializes the module before each test.</summary>
        public async ValueTask InitializeAsync() => await module.InitializeAsync();

        /// <summary>Stops recording and disposes the module after each test.</summary>
        public async ValueTask DisposeAsync()
        {
            activities.Dispose();
            await module.DisposeAsync();
        }

        /// <summary>
        /// Tests that a call is traced as a client activity describing the resource and operation.
        /// </summary>
        [Fact(DisplayName = "A proxied call is traced as a client activity")]
        public async Task ProxiedCallIsTracedAsClientActivity()
        {
            // Arrange
            var mailer = new FakeMailer();
            var proxy = new MailerProxy(mailer, module.GetRequiredService<IModuleTelemetry>());

            // Act
            await proxy.SendAsync("hello");

            // Assert
            Assert.Equal(["hello"], mailer.Sent);
            var activity = Assert.Single(activities.Activities);
            Assert.Equal("smtp Send", activity.DisplayName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
            Assert.Equal(ModuleName, activity.GetTagItem(PolochonTelemetry.ModuleTag));
            Assert.Equal("smtp", activity.GetTagItem(PolochonTelemetry.DependencyTypeTag));
            Assert.Equal("outbound-mail", activity.GetTagItem(PolochonTelemetry.DependencyNameTag));
            Assert.Equal("Send", activity.GetTagItem(PolochonTelemetry.DependencyOperationTag));
        }

        /// <summary>
        /// Tests that a failed call is recorded on its activity once, then rethrown untouched.
        /// </summary>
        [Fact(DisplayName = "A failed proxied call is recorded on its activity and rethrown")]
        public async Task FailedProxiedCallIsRecordedOnItsActivityAndRethrown()
        {
            // Arrange
            var proxy = new MailerProxy(new FakeMailer { Fail = true }, module.GetRequiredService<IModuleTelemetry>());

            // Act
            _ = await Assert.ThrowsAsync<TimeoutException>(() => proxy.SendAsync("hello"));

            // Assert
            var activity = Assert.Single(activities.Activities);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("System.TimeoutException", activity.GetTagItem(PolochonTelemetry.ErrorTypeTag));
            Assert.Single(activity.Events, activityEvent => activityEvent.Name == "exception");
        }

        /// <summary>
        /// Tests that synchronous and asynchronous calls are timed in the dependency duration histogram,
        /// failures carrying their error type.
        /// </summary>
        [Fact(DisplayName = "Proxied calls are timed in the dependency duration histogram")]
        public async Task ProxiedCallsAreTimedInDependencyDurationHistogram()
        {
            // Arrange
            var telemetry = module.GetRequiredService<IModuleTelemetry>();
            using var collector = new MetricCollector<double>(telemetry.Meter, PolochonTelemetry.DependencyDurationMetric);
            var mailer = new FakeMailer();
            var proxy = new MailerProxy(mailer, telemetry);

            // Act
            await proxy.SendAsync("hello");
            var pending = proxy.CountPending();
            mailer.Fail = true;
            _ = await Assert.ThrowsAsync<TimeoutException>(() => proxy.SendAsync("again"));

            // Assert
            Assert.Equal(1, pending);
            var measurements = collector.GetMeasurementSnapshot();
            Assert.Equal(3, measurements.Count);
            Assert.Equal(["Send", "CountPending", "Send"], measurements.Select(measurement => measurement.Tags[PolochonTelemetry.DependencyOperationTag]));
            Assert.False(measurements[0].Tags.ContainsKey(PolochonTelemetry.ErrorTypeTag));
            Assert.Equal("System.TimeoutException", measurements[2].Tags[PolochonTelemetry.ErrorTypeTag]);
        }

        /// <summary>
        /// A stand-in for an external resource with no telemetry of its own.
        /// </summary>
        public sealed class FakeMailer
        {
            /// <summary>Gets the messages sent so far.</summary>
            public List<string> Sent { get; } = [];

            /// <summary>Gets or sets a value indicating whether calls time out.</summary>
            public bool Fail { get; set; }

            /// <summary>Sends a message.</summary>
            /// <param name="message">The message.</param>
            /// <returns>A task completing once sent.</returns>
            public Task SendAsync(string message)
            {
                if (Fail)
                {
                    throw new TimeoutException("Probe timeout.");
                }

                Sent.Add(message);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Proxy tracing every <see cref="FakeMailer"/> call.
        /// </summary>
        public sealed class MailerProxy : ExternalResourceProxy<FakeMailer>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MailerProxy"/> class.
            /// </summary>
            /// <param name="mailer">The mailer to call.</param>
            /// <param name="telemetry">The calling module's telemetry.</param>
            public MailerProxy(FakeMailer mailer, IModuleTelemetry telemetry)
                : base(mailer, telemetry, "smtp", "outbound-mail")
            {
            }

            /// <summary>Sends a message through the mailer.</summary>
            /// <param name="message">The message.</param>
            /// <returns>A task completing once sent.</returns>
            public Task SendAsync(string message) => TelemetryCallAsync("Send", mailer => mailer.SendAsync(message));

            /// <summary>Counts the messages sent so far.</summary>
            /// <returns>The number of messages sent.</returns>
            public int CountPending() => TelemetryCall("CountPending", mailer => mailer.Sent.Count);
        }
    }
}
