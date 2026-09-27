using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Messaging;
using Polochon.Mediation;
using Xunit;

namespace Polochon.Tests.Mediation
{
    /// <summary>
    /// Tests that <see cref="ServiceRegister"/>'s two <c>AddDispatcher</c> overloads register the
    /// same set of dispatcher services, since both funnel through the same private registration
    /// core.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        /// <summary>The <c>Type[]</c> overload registers the notification publisher and the outbox.</summary>
        [Fact]
        public void AddDispatcher_WithTypeArray_RegistersNotificationPublisherAndOutbox()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IPolochonDispatcher>());
            Assert.NotNull(provider.GetService<INotificationPublisher>());
            Assert.NotNull(provider.GetService<IOutbox>());
        }

        /// <summary>The <c>Assembly</c> overload registers the notification publisher and the outbox.</summary>
        [Fact]
        public void AddDispatcher_WithAssembly_RegistersNotificationPublisherAndOutbox()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(typeof(ServiceRegisterTests).Assembly);

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IPolochonDispatcher>());
            Assert.NotNull(provider.GetService<INotificationPublisher>());
            Assert.NotNull(provider.GetService<IOutbox>());
        }

        /// <summary>The outbox is registered as a single shared instance (singleton).</summary>
        [Fact]
        public void AddDispatcher_RegistersOutbox_AsASingleSharedInstance()
        {
            var services = new ServiceCollection();
            services.AddDispatcher(Type.EmptyTypes);

            var provider = services.BuildServiceProvider();

            Assert.Same(provider.GetRequiredService<IOutbox>(), provider.GetRequiredService<IOutbox>());
        }
    }
}
