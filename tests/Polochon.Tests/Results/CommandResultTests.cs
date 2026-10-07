using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Results;
using Xunit;

namespace Polochon.Tests.Results
{
    /// <summary>
    /// Tests for <see cref="ResultCode"/> and <see cref="CommandResult"/>: zero or positive codes
    /// report a success, negative codes a failure.
    /// </summary>
    public sealed class CommandResultTests
    {
        private static readonly ResultCode Created = new() { Code = 1, Status = "CREATED" };

        private static readonly ResultCode Rejected = new() { Code = -100, Status = "REJECTED" };

        /// <summary>Zero or positive codes are successes, negative codes are failures.</summary>
        [Theory(DisplayName = "Zero or positive codes are successes, negative codes are failures")]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(1000, true)]
        [InlineData(-1, false)]
        [InlineData(-1000, false)]
        public void IsOkFollowsTheSignOfTheCode(int code, bool expected)
        {
            Assert.Equal(expected, new ResultCode { Code = code, Status = "ANY" }.IsOk);
        }

        /// <summary>Success with a business code reports that code.</summary>
        [Fact(DisplayName = "Success with a business code reports that code")]
        public void SuccessWithCodeReportsCode()
        {
            var result = CommandResult.Success(Created);

            Assert.True(result.IsSuccess);
            Assert.Equal(Created, result.Result);
            Assert.Empty(result.Errors);
        }

        /// <summary>Success rejects a failure code.</summary>
        [Fact(DisplayName = "Success rejects a failure code")]
        public void SuccessWithFailureCodeThrows()
        {
            _ = Assert.Throws<ArgumentException>(() => CommandResult.Success(Rejected));
        }

        /// <summary>Failure rejects a success code.</summary>
        [Fact(DisplayName = "Failure rejects a success code")]
        public void FailureWithSuccessCodeThrows()
        {
            _ = Assert.Throws<ArgumentException>(() => CommandResult.Failure(Created));
        }

        /// <summary>Failure keeps the given validation errors.</summary>
        [Fact(DisplayName = "Failure keeps the given validation errors")]
        public void FailureKeepsErrors()
        {
            ValidationError[] errors = [new() { Code = Rejected, Message = "rejected", MemberName = "Name" }];

            var result = CommandResult.Failure(Rejected, errors);

            Assert.False(result.IsSuccess);
            Assert.Equal(Rejected, result.Result);
            Assert.Equal(errors, result.Errors);
        }
    }
}
