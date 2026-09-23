using Polochon.Abstractions.Results;

namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Validator interface for validating messages before they are handled.
    /// Validators run after pipeline behaviors but before the actual message handler.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message to validate.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IMessageValidator<in TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        /// <summary>
        /// Validates the message asynchronously.
        /// If validation fails, this method should throw a <see cref="MessageValidationException"/>.
        /// The exception blocks message processing (the handler is not called) and propagates
        /// to the caller of the dispatcher, so it knows the message was rejected.
        /// </summary>
        /// <param name="request">The request message to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A ValueTask representing the asynchronous validation operation.</returns>
        /// <exception cref="MessageValidationException">Thrown when validation fails.</exception>
        ValueTask ValidateAsync(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Exception thrown when message validation fails, e.g. when the sender is not allowed to
    /// send the message. The message handling pipeline does not catch it: the handler is not
    /// called and the exception propagates to the caller of the dispatcher.
    /// </summary>
    public sealed class MessageValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageValidationException"/> class.
        /// </summary>
        public MessageValidationException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageValidationException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public MessageValidationException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageValidationException"/> class
        /// with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public MessageValidationException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageValidationException"/> class
        /// with the code reported to the caller of a command returning a <see cref="CommandResult"/>.
        /// </summary>
        /// <param name="error">The code reporting why the message was rejected.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public MessageValidationException(ResultCode error, string message) : base(message)
        {
            Error = error;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageValidationException"/> class
        /// from every failure a <see cref="MessageValidator{TRequest, TResponse}"/> found: the first
        /// one is reported, all of them are kept in <see cref="Errors"/>.
        /// </summary>
        /// <param name="errors">Every failure, in rule order. Must not be empty.</param>
        public MessageValidationException(IReadOnlyList<ValidationError> errors)
            : base(FirstOf(errors).Message)
        {
            Error = errors[0].Code;
            Errors = errors;
        }

        /// <summary>
        /// The code reporting why the message was rejected, when the validator gave one.
        /// Without it, a command returning a <see cref="CommandResult"/> reports
        /// <see cref="ResultCode.ValidationFailed"/>.
        /// </summary>
        public ResultCode? Error { get; }

        /// <summary>
        /// Every failure found, in rule order, when the message was rejected by a
        /// <see cref="MessageValidator{TRequest, TResponse}"/>; empty otherwise.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; } = [];

        private static ValidationError FirstOf(IReadOnlyList<ValidationError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            return errors.Count > 0
                ? errors[0]
                : throw new ArgumentException("At least one validation error is required.", nameof(errors));
        }
    }
}