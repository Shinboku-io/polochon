using Polochon.Abstractions.CQRS;

namespace Polochon.Tests.TestModule.Queries
{
    /// <summary>
    /// Test query for demonstrating the CQRS pattern in a module.
    /// </summary>
    public record TestQuery : IQuery<TestQueryResult>
    {
        /// <summary>
        /// Gets or sets the input value for the query.
        /// </summary>
        public string Input { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test query for demonstrating the CQRS pattern in a module.
    /// </summary>
    public record TestQueryWithValidation : IQuery<TestQueryResult>
    {
        /// <summary>
        /// Gets or sets the input value for the query.
        /// </summary>
        public string Input { get; set; } = string.Empty;
    }

    /// <summary>
    /// The result type for the TestQuery.
    /// </summary>
    public record TestQueryResult
    {
        /// <summary>
        /// Gets or sets the output value.
        /// </summary>
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the processed flag.
        /// </summary>
        public bool Processed { get; set; }
    }
}