namespace Polochon.Messaging.AzureQueue
{
    /// <summary>
    /// Wire format for a message sent to the error queue: the original message - as best as it
    /// could be captured - plus what went wrong reading or processing it.
    /// </summary>
    public class ErrorMessageEnvelope
    {
        /// <summary>
        /// The original message's type, in the same "Namespace.ClassName, AssemblyName" format as
        /// <see cref="MessageEnvelope.MessageType"/> - or <c>"unknown"</c> if the message could not
        /// be identified at all (e.g. its envelope was invalid).
        /// </summary>
        public required string MessageType { get; set; }

        /// <summary>The original message's raw payload, kept for inspection and replay.</summary>
        public required string Payload { get; set; }

        /// <summary>The exception (message and stack trace) that caused this message to fail.</summary>
        public required string Error { get; set; }

        /// <summary>When this message was moved to the error queue.</summary>
        public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
