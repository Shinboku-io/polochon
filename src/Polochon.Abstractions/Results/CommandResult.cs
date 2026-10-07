using Polochon.Abstractions.CQRS;

namespace Polochon.Abstractions.Results
{
    /// <summary>
    /// Result of a command, as reported to its caller: a success code (<see cref="ResultCode.Ok"/>
    /// or a business success code), or the code of the issue. Commands returning it never throw
    /// expected failures at their caller: the dispatcher's result behavior turns them into a failed
    /// result instead.
    /// </summary>
    public sealed record CommandResult
    {
        /// <summary>The outcome of the command.</summary>
        public required ResultCode Result { get; init; }

        /// <summary>
        /// Every validation failure found when a validator rejected the command, in rule order;
        /// empty otherwise.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; init; } = [];

        /// <summary>Whether the command succeeded.</summary>
        public bool IsSuccess => Result.IsOk;

        /// <summary>Creates a successful result reporting <see cref="ResultCode.Ok"/>.</summary>
        public static CommandResult Success() => new() { Result = ResultCode.Ok };

        /// <summary>Creates a successful result reporting a business success <paramref name="code"/>.</summary>
        /// <param name="code">A success code, zero or positive.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> reports a failure.</exception>
        public static CommandResult Success(ResultCode code)
        {
            ArgumentNullException.ThrowIfNull(code);

            return code.IsOk
                ? new() { Result = code }
                : throw new ArgumentException($"'{code.Status}' ({code.Code}) is not a success code.", nameof(code));
        }

        /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
        /// <param name="error">The code reporting the failure, negative.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> reports a success.</exception>
        public static CommandResult Failure(ResultCode error) => Failure(error, []);

        /// <summary>Creates a failed result carrying <paramref name="error"/> and every validation failure found.</summary>
        /// <param name="error">The code reporting the failure, negative.</param>
        /// <param name="errors">Every validation failure found, in rule order.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> reports a success.</exception>
        public static CommandResult Failure(ResultCode error, IReadOnlyList<ValidationError> errors)
        {
            ArgumentNullException.ThrowIfNull(error);
            ArgumentNullException.ThrowIfNull(errors);

            return !error.IsOk
                ? new() { Result = error, Errors = errors }
                : throw new ArgumentException($"'{error.Status}' ({error.Code}) is not a failure code.", nameof(error));
        }
    }
}
