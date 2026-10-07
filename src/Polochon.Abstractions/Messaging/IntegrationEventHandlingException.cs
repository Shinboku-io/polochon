using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Results;

namespace Polochon.Abstractions.Messaging
{
    /// <summary>
    /// Thrown by <see cref="IntegrationEventToCommandHandler{TEvent, TCommand}"/> when the command
    /// an integration event was mapped to reports a failure, so the event is reported as failed
    /// instead of being silently dropped.
    /// </summary>
    public sealed class IntegrationEventHandlingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationEventHandlingException"/> class.
        /// </summary>
        public IntegrationEventHandlingException()
        {
            Result = CommandResult.Failure(ResultCode.UnexpectedError);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationEventHandlingException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public IntegrationEventHandlingException(string message)
            : base(message)
        {
            Result = CommandResult.Failure(ResultCode.UnexpectedError);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationEventHandlingException"/> class
        /// with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public IntegrationEventHandlingException(string message, Exception innerException)
            : base(message, innerException)
        {
            Result = CommandResult.Failure(ResultCode.UnexpectedError);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationEventHandlingException"/> class
        /// for an event whose command reported <paramref name="result"/>.
        /// </summary>
        /// <param name="integrationEvent">The event that failed.</param>
        /// <param name="result">The failed result of the command the event was mapped to.</param>
        public IntegrationEventHandlingException(IIntegrationEvent integrationEvent, CommandResult result)
            : base($"Handling integration event '{integrationEvent?.GetType().Name}' failed with '{result?.Result.Status}'.")
        {
            ArgumentNullException.ThrowIfNull(integrationEvent);
            ArgumentNullException.ThrowIfNull(result);

            Result = result;
        }

        /// <summary>The failed result of the command the event was mapped to.</summary>
        public CommandResult Result { get; }
    }
}
