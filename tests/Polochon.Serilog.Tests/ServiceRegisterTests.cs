using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Polochon;
using Polochon.Abstractions.Modules;
using Polochon.Modules;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Polochon.Serilog.Tests
{
    /// <summary>
    /// Tests for <see cref="ServiceRegister.WithSerilog(IServiceCollection)"/> and its overloads.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        private sealed class CapturingSink : ILogEventSink
        {
            public LogEvent? LastEvent { get; private set; }

            public void Emit(LogEvent logEvent) => LastEvent = logEvent;
        }

        /// <summary>
        /// Tests that WithSerilog() registers a working Microsoft.Extensions.Logging pipeline backed by Serilog.
        /// </summary>
        [Fact]
        public void WithSerilog_RegistersLoggerFactory()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.WithSerilog();
            using var provider = services.BuildServiceProvider();

            // Assert
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Polochon.Serilog.Tests");
            Assert.NotNull(logger);
        }

        /// <summary>
        /// Tests that WithSerilog() sets the global Serilog logger once the logging pipeline is
        /// actually resolved (registration is lazy: the underlying Serilog.Extensions.Hosting
        /// integration only builds the logger when something first resolves ILoggerFactory/ILogger).
        /// </summary>
        [Fact]
        public void WithSerilog_SetsGlobalSerilogLogger()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.WithSerilog();
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<ILoggerFactory>();

            // Assert
            Assert.IsType<global::Serilog.Core.Logger>(Log.Logger);
        }

        /// <summary>
        /// Tests that WithSerilog(configureLogger) invokes the provided callback once the logging
        /// pipeline is resolved, to customize the configuration.
        /// </summary>
        [Fact]
        public void WithSerilog_WithConfigureCallback_InvokesCallback()
        {
            // Arrange
            var services = new ServiceCollection();
            var wasCalled = false;

            _ = services.WithSerilog(configuration =>
            {
                wasCalled = true;
                configuration.MinimumLevel.Debug();
            });

            // Act
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<ILoggerFactory>();

            // Assert
            Assert.True(wasCalled);
        }

        /// <summary>
        /// Tests that WithSerilog(configureLogger) throws when the callback is null.
        /// </summary>
        [Fact]
        public void WithSerilog_WithNullCallback_Throws()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.WithSerilog(null!));
        }

        /// <summary>
        /// Tests that WithSerilog() returns the same service collection instance for chaining.
        /// </summary>
        [Fact]
        public void WithSerilog_ReturnsSameServiceCollection()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.WithSerilog();

            // Assert
            Assert.Same(services, result);
        }

        /// <summary>
        /// Tests the full pattern this overload exists for: services.AddModule&lt;TModule&gt;().WithSerilog()
        /// gives the module a working Serilog-backed logger, without touching the global Log.Logger.
        /// </summary>
        [Fact]
        public async Task ModuleBuilder_WithSerilog_GivesModuleAWorkingSerilogLogger()
        {
            // Arrange
            var services = new ServiceCollection();
            var globalLoggerBefore = Log.Logger;

            var builder = services.AddModule<FakeModule>();
            _ = builder.WithSerilog(config => config.MinimumLevel.Debug());

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();

            // Act
            await module.InitializeAsync();

            // Assert - the module resolves a working, provider-backed logger...
            var loggerFactory = module.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Polochon.Serilog.Tests");
            Assert.True(logger.IsEnabled(LogLevel.Debug));

            // ...and the global Serilog logger was left untouched (module-level config is isolated).
            Assert.Same(globalLoggerBefore, Log.Logger);
        }

        /// <summary>
        /// Tests that IModularModuleBuilder.WithSerilog() tags every emitted log event with the
        /// module's own Name ("fake" for FakeModule), derived automatically rather than requiring
        /// the caller to repeat it in the configureLogger callback.
        /// </summary>
        [Fact]
        public async Task ModuleBuilder_WithSerilog_TagsLogEventsWithModuleName()
        {
            // Arrange
            var services = new ServiceCollection();
            var sink = new CapturingSink();

            var builder = services.AddModule<FakeModule>();
            _ = builder.WithSerilog(config => config.WriteTo.Sink(sink));

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            var loggerFactory = module.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Polochon.Serilog.Tests");

            // Act
            logger.LogInformation("test message");

            // Assert
            Assert.NotNull(sink.LastEvent);
            var moduleProperty = Assert.Contains("Module", sink.LastEvent.Properties);
            Assert.Equal("fake", moduleProperty.ToString().Trim('"'));
        }

        /// <summary>
        /// Tests that IModularModuleBuilder.WithSerilog() returns the same builder instance for chaining.
        /// </summary>
        [Fact]
        public void ModuleBuilder_WithSerilog_ReturnsSameBuilderInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();

            // Act
            var result = builder.WithSerilog();

            // Assert
            Assert.Same(builder, result);
        }

        /// <summary>
        /// Tests that with host-level Serilog, other logging providers still receive events, while
        /// Polochon's base console provider is removed so the console does not print every event twice.
        /// </summary>
        [Fact(DisplayName = "Host WithSerilog writes to other providers but drops the base console provider")]
        public void HostWithSerilogWritesToOtherProvidersButDropsBaseConsoleProvider()
        {
            // Arrange
            var capture = new CapturingLoggerProvider();
            var services = new ServiceCollection();
            _ = services.AddPolochon();
            _ = services.AddSingleton<ILoggerProvider>(capture);

            // Act
            _ = services.WithSerilog();
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("Polochon.Serilog.Tests").LogInformation("host event");

            // Assert
            Assert.Contains("host event", capture.Messages);
            Assert.DoesNotContain(provider.GetServices<ILoggerProvider>(), loggerProvider => loggerProvider is ConsoleLoggerProvider);
        }

        /// <summary>
        /// Tests that with module-level Serilog, other providers registered in the module - before or after
        /// WithSerilog() - still receive events, while the base console provider is removed.
        /// </summary>
        [Fact(DisplayName = "Module WithSerilog writes to other providers but drops the base console provider")]
        public async Task ModuleWithSerilogWritesToOtherProvidersButDropsBaseConsoleProvider()
        {
            // Arrange
            var registeredBefore = new CapturingLoggerProvider();
            var registeredAfter = new CapturingLoggerProvider();
            var services = new ServiceCollection();
            _ = services.AddModule<FakeModule>()
                .ConfigureModule((moduleServices, _, _) => moduleServices.AddSingleton<ILoggerProvider>(registeredBefore))
                .WithSerilog()
                .ConfigureModule((moduleServices, _, _) => moduleServices.AddSingleton<ILoggerProvider>(registeredAfter));
            using var host = services.BuildServiceProvider();
            var module = (FakeModule)host.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            // Act
            module.GetRequiredService<ILoggerFactory>().CreateLogger("Polochon.Serilog.Tests").LogInformation("module event");

            // Assert
            Assert.Contains("module event", registeredBefore.Messages);
            Assert.Contains("module event", registeredAfter.Messages);
            Assert.DoesNotContain(module.GetRequiredService<IEnumerable<ILoggerProvider>>(), loggerProvider => loggerProvider is ConsoleLoggerProvider);
        }
    }
}
