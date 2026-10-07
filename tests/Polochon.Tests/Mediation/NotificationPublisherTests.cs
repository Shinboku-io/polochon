using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Mediation;
using Xunit;

namespace Polochon.Tests.Mediation
{
    /// <summary>
    /// Tests for <see cref="NotificationPublisher"/>, in particular that publishing a notification
    /// with no registered handler is a no-op rather than a failure - notifications are pub/sub,
    /// unlike the exactly-one-handler contract of commands/queries.
    /// </summary>
    public sealed class NotificationPublisherTests
    {
        /// <summary>A notification used only by these tests.</summary>
        public sealed record TestNotification : INotification
        {
            /// <summary>Creates the notification with the given payload.</summary>
            public TestNotification(string payload)
            {
                Payload = payload;
            }

            /// <summary>An arbitrary marker value used to identify this notification in assertions.</summary>
            public string Payload { get; init; }

            /// <inheritdoc/>
            public Guid Id { get; init; } = Guid.NewGuid();

            /// <inheritdoc/>
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        }

        /// <summary>Shared, DI-resolved sink that handlers append to, so tests can assert on it.</summary>
        public sealed class Recorder
        {
            /// <summary>The payloads received so far, in handling order.</summary>
            public List<string> Received { get; } = [];
        }

        /// <summary>A handler that records every notification it receives.</summary>
        public sealed class RecordingHandler : INotificationHandler<TestNotification>
        {
            private readonly Recorder recorder;

            /// <summary>Creates the handler with the shared recorder to append to.</summary>
            public RecordingHandler(Recorder recorder)
            {
                this.recorder = recorder;
            }

            /// <inheritdoc/>
            public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                recorder.Received.Add(notification.Payload);
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>A second, distinct handler type for the same notification, to test fan-out.</summary>
        public sealed class SecondRecordingHandler : INotificationHandler<TestNotification>
        {
            private readonly Recorder recorder;

            /// <summary>Creates the handler with the shared recorder to append to.</summary>
            public SecondRecordingHandler(Recorder recorder)
            {
                this.recorder = recorder;
            }

            /// <inheritdoc/>
            public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                recorder.Received.Add($"second:{notification.Payload}");
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>Publishing with zero subscribers must complete normally, not throw.</summary>
        [Fact]
        public async Task PublishAsync_WithNoRegisteredHandler_DoesNotThrow()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Should complete without throwing, even though nothing subscribes to TestNotification.
            await publisher.PublishAsync(new TestNotification("nobody is listening"));
        }

        /// <summary>Publishing with one registered handler invokes it.</summary>
        [Fact]
        public async Task PublishAsync_WithOneRegisteredHandler_InvokesIt()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher(typeof(RecordingHandler));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            await publisher.PublishAsync(new TestNotification("hello"));

            Assert.Equal(["hello"], provider.GetRequiredService<Recorder>().Received);
        }

        /// <summary>Publishing with several registered handlers invokes every one of them.</summary>
        [Fact]
        public async Task PublishAsync_WithMultipleRegisteredHandlers_InvokesAll()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher(typeof(RecordingHandler), typeof(SecondRecordingHandler));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            await publisher.PublishAsync(new TestNotification("broadcast"));

            Assert.Equal(["broadcast", "second:broadcast"], provider.GetRequiredService<Recorder>().Received);
        }
    }
}
