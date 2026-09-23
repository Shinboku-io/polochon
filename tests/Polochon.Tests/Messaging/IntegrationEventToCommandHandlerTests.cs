using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Mediation;
using Polochon.Tests.Domain;
using Xunit;

namespace Polochon.Tests.Messaging
{
    /// <summary>
    /// Tests for <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/>: a derived class
    /// is discovered and wired up by the same <c>AddDispatcher</c> scan as any other
    /// <see cref="INotificationHandler{TNotification}"/> - no dedicated registration is needed.
    /// </summary>
    public sealed class IntegrationEventToCommandHandlerTests
    {
        /// <summary>The command <see cref="TestEventToCommandHandler"/> maps <see cref="TestIntegrationEvent"/> to.</summary>
        public sealed record TestCommand : ICommand
        {
            /// <summary>Creates the command with the given payload.</summary>
            public TestCommand(string payload)
            {
                Payload = payload;
            }

            /// <summary>An arbitrary marker value, carried over from the source event.</summary>
            public string Payload { get; init; }
        }

        /// <summary>Shared, DI-resolved sink that the command handler appends to, so tests can assert on it.</summary>
        public sealed class Recorder
        {
            /// <summary>The payloads received so far, in handling order.</summary>
            public List<string> Received { get; } = [];
        }

        /// <summary>Handles the mapped command by recording it - stands in for real business logic.</summary>
        public sealed class TestCommandHandler : ICommandHandler<TestCommand, Unit>
        {
            private readonly Recorder recorder;

            /// <summary>Creates the handler with the shared recorder to append to.</summary>
            public TestCommandHandler(Recorder recorder)
            {
                this.recorder = recorder;
            }

            /// <inheritdoc/>
            public ValueTask<Unit> HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
            {
                recorder.Received.Add(command.Payload);
                return ValueTask.FromResult(default(Unit));
            }
        }

        /// <summary>Translates <see cref="TestIntegrationEvent"/> into <see cref="TestCommand"/>.</summary>
        public sealed class TestEventToCommandHandler : IntegrationEventToCommandHandler<TestIntegrationEvent, TestCommand>
        {
            /// <summary>Creates the handler, forwarding the dispatcher to the base class.</summary>
            public TestEventToCommandHandler(IPolochonDispatcher dispatcher)
                : base(dispatcher)
            {
            }

            /// <inheritdoc/>
            protected override TestCommand Map(TestIntegrationEvent integrationEvent)
                => new(integrationEvent.Payload);
        }

        /// <summary>
        /// Publishing the integration event ends up invoking the mapped command's own handler -
        /// proving the event was translated and dispatched, not handled inline.
        /// </summary>
        [Fact]
        public async Task PublishAsync_WithIntegrationEventToCommandHandlerRegistered_DispatchesMappedCommand()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher([typeof(TestEventToCommandHandler), typeof(TestCommandHandler)]);

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            await publisher.PublishAsync(new TestIntegrationEvent("from-another-module"));

            Assert.Equal(["from-another-module"], provider.GetRequiredService<Recorder>().Received);
        }

        /// <summary>
        /// A module registering an <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/>
        /// is reported as able to handle that integration event, the same as it would be for a
        /// plain <see cref="INotificationHandler{TNotification}"/> - used by inbox routing.
        /// </summary>
        [Fact]
        public void DispatcherRegistry_WithIntegrationEventToCommandHandlerRegistered_IncludesEventType()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Recorder>();
            services.AddDispatcher([typeof(TestEventToCommandHandler), typeof(TestCommandHandler)]);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<DispatcherRegistry>();

            Assert.True(registry.NotificationWrappers.ContainsKey(typeof(TestIntegrationEvent)));
        }
    }
}
