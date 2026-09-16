using Polochon.Abstractions.CQRS;

namespace Polochon.Tests.TestModule.Queries
{
    /// <summary>
    /// Handler for the TestQuery.
    /// This demonstrates how to handle queries in a module using the Mediator library.
    /// Handlers are auto-discovered by Mediator - no direct reference to Mediator implementation needed in TestModule.
    /// </summary>
    public partial class TestQueryHandler : IQueryHandler<TestQuery, TestQueryResult>
    {
        public async ValueTask<TestQueryResult> HandleAsync(TestQuery query, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);

            return new TestQueryResult
            {
                Output = query.Input.ToUpperInvariant(),
                Processed = true
            };
        }
    }

    /// <summary>
    /// Handler for the TestQuery.
    /// This demonstrates how to handle queries in a module using the Mediator library.
    /// Handlers are auto-discovered by Mediator - no direct reference to Mediator implementation needed in TestModule.
    /// </summary>
    public partial class TestQueryHandlerAfterValidation : IQueryHandler<TestQueryWithValidation, TestQueryResult>
    {
        public async ValueTask<TestQueryResult> HandleAsync(TestQueryWithValidation query, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);

            return new TestQueryResult
            {
                Output = query.Input.ToUpperInvariant(),
                Processed = true
            };
        }
    }
}