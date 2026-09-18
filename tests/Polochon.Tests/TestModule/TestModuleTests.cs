using Microsoft.Extensions.Logging;
using Polochon.Abstractions.Modules;
using Polochon.Tests.TestModule.Queries;
using Xunit;

namespace Polochon.Tests.TestModule
{
    /// <summary>
    /// Tests for the TestModule demonstrating that:
    /// 1. A TestModule inheriting from ModuleBase works correctly
    /// 2. A TestQuery sent to the module via IModularModule.SendQueryAsync is handled by TestQueryHandler
    /// 3. The TestModule can register its handler without directly referencing Mediator library implementation logic
    /// </summary>
    public sealed class TestModuleTests : IAsyncLifetime
    {
        private readonly TestModule _module = new();

        /// <summary>
        /// Initializes the module before each test.
        /// </summary>
        public async ValueTask InitializeAsync()
        {
            await _module.InitializeAsync();
        }

        /// <summary>
        /// Disposes the module after each test.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _module.DisposeAsync();
        }

        /// <summary>
        /// Tests that the module can be initialized.
        /// </summary>
        [Fact]
        public void Module_Initializes_Correctly()
        {
            // Arrange
            var module = new TestModule();

            // Act
            module.Initialize();

            // Assert
            Assert.NotNull(module.ServiceProvider);
            Assert.Equal("TestModule", module.Name);

            // Cleanup
            module.Dispose();
        }

        /// <summary>
        /// Tests that every module gets a working logging pipeline out of the box, without the
        /// module author having to configure it (ModuleBase.ConfigureServices wires it in).
        /// </summary>
        [Fact]
        public void Module_ResolvesLoggerFactory_WithAProviderAttached()
        {
            // Assert - a logger backed by no provider would report IsEnabled == false
            var loggerFactory = _module.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Polochon.Tests");
            Assert.True(logger.IsEnabled(LogLevel.Information));
        }

        /// <summary>
        /// Tests that the singleton instance works correctly.
        /// </summary>
        [Fact]
        public void SingletonInstance_Returns_SameInstance()
        {
            // Act
            var instance1 = TestModule.Instance;
            var instance2 = TestModule.Instance;

            // Assert
            Assert.Same(instance1, instance2);
        }

        /// <summary>
        /// Tests that a query sent to the module via IModularModule.SendQueryAsync
        /// is handled by TestQueryHandler defined in the TestModule assembly.
        /// This verifies that:
        /// 1. The TestModule can send queries using the IModularModule interface
        /// 2. The TestQueryHandler (registered via source generation with [MediatorService])
        ///    handles the query without the TestModule directly referencing Mediator library logic
        /// </summary>
        [Fact]
        public async Task Query_SentToModuleViaIModularModule_SendQueryAsync_IsHandledByTestQueryHandler()
        {
            // Arrange
            var query = new TestQuery { Input = "hello world" };

            // Act - Send query using the IModularModule interface method
            // This is the key test: using SendQueryAsync from IModularModule, not directly accessing Mediator
            var result = await ((IModularModule)_module).SendQueryAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("HELLO WORLD", result.Output);
            Assert.True(result.Processed);
        }

        /// <summary>
        /// Tests that multiple queries can be sent sequentially via IModularModule.
        /// </summary>
        [Fact]
        public async Task MultipleQueries_SentSequentiallyViaIModularModule_AllSucceed()
        {
            // Arrange
            var queries = new[]
            {
                new TestQuery { Input = "first" },
                new TestQuery { Input = "second" },
                new TestQuery { Input = "third" }
            };

            // Act - Send all queries via IModularModule interface
            var results = new List<TestQueryResult>();
            foreach (var query in queries)
            {
                var result = await ((IModularModule)_module).SendQueryAsync(query);
                results.Add(result);
            }

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("FIRST", results[0].Output);
            Assert.Equal("SECOND", results[1].Output);
            Assert.Equal("THIRD", results[2].Output);
            Assert.All(results, r => Assert.True(r.Processed));
        }

        /// <summary>
        /// Tests that the module's SendQueryAsync can be used directly (not just via IModularModule interface).
        /// </summary>
        [Fact]
        public async Task Query_SentToModuleDirectly_Works()
        {
            // Arrange
            var query = new TestQuery { Input = "direct test" };

            // Act - Use the module's SendQueryAsync method directly
            var result = await _module.SendQueryAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("DIRECT TEST", result.Output);
            Assert.True(result.Processed);
        }
    }
}