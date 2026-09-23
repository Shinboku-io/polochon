using Polochon.Abstractions.Results;

namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// One broken validation rule, independent of the validation library that detected it.
    /// </summary>
    public sealed record ValidationError
    {
        /// <summary>The code reported to the caller when this is the first failure.</summary>
        public required ResultCode Code { get; init; }

        /// <summary>A message describing the failure, for logs and diagnostics.</summary>
        public required string Message { get; init; }

        /// <summary>The validated member, when the rule applies to one.</summary>
        public string? MemberName { get; init; }
    }
}
