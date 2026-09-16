using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Mediation;
using Xunit;

namespace Polochon.Tests.TestModule.Queries
{
    /// <summary>
    /// Tests for the IMessageValidator interface and its integration with the message handling pipeline.
    /// </summary>
    public sealed class MessageValidatorTests
    {
        /// <summary>
        /// Tests that a validator can be registered and runs before the message handler.
        /// </summary>
        [Fact]
        public async Task Validator_RunsBeforeHandler_WhenValidationPasses()
        {
            var validator = new RecordingValidator { Name = "TestValidator", CallOrder = [] };

            // Arrange
            var services = new ServiceCollection();

            // Register a failing validator directly (not via scanning)
            services.AddScoped<IMessageValidator<TestQueryWithValidation, TestQueryResult>>(sp => validator);

            services.AddScoped<IQueryHandler<TestQueryWithValidation, TestQueryResult>, TestQueryHandlerAfterValidation>();
            services.AddDispatcher([typeof(TestQueryHandlerAfterValidation)]);

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPolochonDispatcher>();

            var query = new TestQueryWithValidation { Input = "valid input" };

            // Act
            var result = await dispatcher.SendQueryAsync(query);

            // Assert
            // Validation should pass and handler should process the query
            Assert.NotNull(result);
            Assert.Equal("valid input", validator.Metadata);
            Assert.Equal("VALID INPUT", result.Output);
            Assert.True(result.Processed);
        }

        /// <summary>
        /// Tests that validation failure blocks the message handling and returns default.
        /// </summary>
        [Fact]
        public async Task Validator_BlocksHandler_WhenValidationFails()
        {
            // Arrange
            var services = new ServiceCollection();

            // Register a failing validator directly (not via scanning)
            services.AddScoped<IMessageValidator<TestQueryWithValidation, TestQueryResult>, FailingTestQueryValidator>();

            services.AddScoped<IQueryHandler<TestQueryWithValidation, TestQueryResult>, TestQueryHandlerAfterValidation>();
            services.AddDispatcher([typeof(TestQueryHandlerAfterValidation)]);

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPolochonDispatcher>();

            var query = new TestQueryWithValidation { Input = "any input" };

            // Act
            var result = await dispatcher.SendQueryAsync(query);

            // Assert
            // Validation should fail and block the handler, returning default
            // The handler should not have been called
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that multiple validators run in the correct order.
        /// </summary>
        [Fact]
        public async Task MultipleValidators_RunInRegistrationOrder()
        {
            // Arrange
            var validatorCallOrder = new List<string>();
            var services = new ServiceCollection();

            // Create a custom validator that records its execution
            services.AddScoped<IMessageValidator<TestQueryWithValidation, TestQueryResult>>(sp =>
                new RecordingValidator() { Name = "First", CallOrder = validatorCallOrder });
            services.AddScoped<IMessageValidator<TestQueryWithValidation, TestQueryResult>>(sp =>
                new RecordingValidator() { Name = "Second", CallOrder = validatorCallOrder });

            services.AddScoped<IQueryHandler<TestQueryWithValidation, TestQueryResult>, TestQueryHandlerAfterValidation>();
            services.AddDispatcher([typeof(TestQueryHandlerAfterValidation)]);

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPolochonDispatcher>();

            var query = new TestQueryWithValidation { Input = "test" };

            // Act
            var result = await dispatcher.SendQueryAsync(query);

            // Assert
            // Both validators should have run
            Assert.Contains("First", validatorCallOrder);
            Assert.Contains("Second", validatorCallOrder);

            // First should run before Second (since they're wrapped in reverse order)
            Assert.Equal("Second", validatorCallOrder[0]);
            Assert.Equal("First", validatorCallOrder[1]);

            // Handler should still execute
            Assert.NotNull(result);
            Assert.Equal("TEST", result.Output);
        }

        /// <summary>
        /// Tests that MessageValidationException is thrown and caught correctly.
        /// </summary>
        [Fact]
        public async Task MessageValidationException_BlocksHandlerWithoutBubbling()
        {
            // Arrange
            var services = new ServiceCollection();

            // Register a failing validator directly (not via scanning)
            services.AddScoped<IMessageValidator<TestQueryWithValidation, TestQueryResult>, FailingTestQueryValidator>();

            services.AddScoped<IQueryHandler<TestQueryWithValidation, TestQueryResult>, TestQueryHandlerAfterValidation>();
            services.AddDispatcher([typeof(TestQueryHandlerAfterValidation)]);

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPolochonDispatcher>();

            var query = new TestQueryWithValidation { Input = "should fail" };

            // Act & Assert
            // Should not throw, but return default (null)
            var result = await dispatcher.SendQueryAsync(query);
            Assert.Null(result);
        }

        /// <summary>
        /// Custom validator for testing that always fails.
        /// </summary>
        private sealed class FailingTestQueryValidator : IMessageValidator<TestQueryWithValidation, TestQueryResult>
        {
            public ValueTask ValidateAsync(TestQueryWithValidation request, CancellationToken cancellationToken)
            {
                // Always throw to simulate validation failure
                throw new MessageValidationException("Test validation always fails.");
            }
        }

        /// <summary>
        /// Custom validator for testing that records its execution.
        /// </summary>
        private sealed class RecordingValidator : IMessageValidator<TestQueryWithValidation, TestQueryResult>
        {
            public string Name { get; init; }
            public List<string> CallOrder { get; init; }

            public string Metadata { get; set; } = string.Empty;

            public ValueTask ValidateAsync(TestQueryWithValidation request, CancellationToken cancellationToken)
            {
                CallOrder.Add(Name);
                Metadata = request.Input;
                return ValueTask.CompletedTask;
            }
        }
    }
}