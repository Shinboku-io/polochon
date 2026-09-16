using Polochon.Abstractions.CQRS;

namespace Polochon.Tests.TestModule.Queries
{
    /// <summary>
    /// Test validator for TestQuery that validates the input is not null or empty.
    /// </summary>
    public sealed class TestQueryValidator : IMessageValidator<TestQuery, TestQueryResult>
    {
        /// <summary>
        /// Validates the TestQuery asynchronously.
        /// </summary>
        /// <param name="request">The query to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A ValueTask representing the asynchronous validation operation.</returns>
        /// <exception cref="MessageValidationException">Thrown when validation fails.</exception>
        public ValueTask ValidateAsync(TestQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Input))
            {
                throw new MessageValidationException("Input cannot be null or empty.");
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Test validator for TestQuery that always passes validation.
    /// </summary>
    public sealed class TestQueryPassingValidator : IMessageValidator<TestQuery, TestQueryResult>
    {
        /// <summary>
        /// Validates the TestQuery asynchronously (always passes).
        /// </summary>
        /// <param name="request">The query to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A ValueTask representing the asynchronous validation operation.</returns>
        public ValueTask ValidateAsync(TestQuery request, CancellationToken cancellationToken)
        {
            // Always passes validation
            return ValueTask.CompletedTask;
        }
    }
}
