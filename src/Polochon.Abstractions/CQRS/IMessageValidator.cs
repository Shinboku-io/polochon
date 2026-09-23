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
    }
}