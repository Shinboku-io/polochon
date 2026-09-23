namespace Polochon.Abstractions.Results
{
    /// <summary>
    /// Thrown when a business rule is violated, e.g. by an aggregate's guard clause. Carries the
    /// <see cref="ResultCode"/> reported to the caller of a command returning an
    /// <see cref="CommandResult"/>.
    /// </summary>
    public sealed class BusinessRuleException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleException"/> class.
        /// </summary>
        /// <param name="error">The code reporting the violated rule.</param>
        public BusinessRuleException(ResultCode error)
            : base($"{error.Code}/{error.Status}")
        {
            Error = error;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleException"/> class with a
        /// detailed message.
        /// </summary>
        /// <param name="error">The code reporting the violated rule.</param>
        /// <param name="message">A message describing the violation, for logs and diagnostics.</param>
        public BusinessRuleException(ResultCode error, string message)
            : base(message)
        {
            Error = error;
        }

        /// <summary>The code reporting the violated rule.</summary>
        public ResultCode Error { get; }
    }
}
