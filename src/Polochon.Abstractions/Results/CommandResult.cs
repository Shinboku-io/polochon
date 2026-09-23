namespace Polochon.Abstractions.Results
{
    /// <summary>
    /// Result of a command, as reported to its caller: <see cref="ResultCode.Ok"/> on success, or
    /// the code of the issue. Commands returning it never throw expected failures at their caller:
    /// the dispatcher's result behavior turns them into a failed result instead.
    /// </summary>
    public sealed record CommandResult
    {
        /// <summary>The outcome of the command.</summary>
        public required ResultCode Result { get; init; }

        /// <summary>Whether the command succeeded.</summary>
        public bool IsSuccess => Result.IsOk;

        /// <summary>Creates a successful result.</summary>
        public static CommandResult Success() => new() { Result = ResultCode.Ok };

        /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
        /// <param name="error">The code reporting the failure.</param>
        public static CommandResult Failure(ResultCode error) => new() { Result = error };
    }
}
