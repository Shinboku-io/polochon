using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polochon.Logging;
using Xunit;

namespace Polochon.Tests.Logging
{
    /// <summary>
    /// Tests for <see cref="Polochon.Logging.ServiceRegister.AddPolochonLogging(IServiceCollection)"/>.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        /// <summary>
        /// Tests that AddPolochonLogging registers a working Microsoft.Extensions.Logging pipeline.
        /// </summary>
        [Fact]
        public void AddPolochonLogging_RegistersLoggerFactory()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.AddPolochonLogging();
            using var provider = services.BuildServiceProvider();

            // Assert
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Polochon.Tests");
            Assert.NotNull(logger);
        }

        /// <summary>
        /// Tests that AddPolochonLogging returns the same service collection instance for chaining.
        /// </summary>
        [Fact]
        public void AddPolochonLogging_ReturnsSameServiceCollection()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddPolochonLogging();

            // Assert
            Assert.Same(services, result);
        }
    }
}
