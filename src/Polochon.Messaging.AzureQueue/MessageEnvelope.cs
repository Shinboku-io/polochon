namespace Polochon.Messaging.AzureQueue
{
    /// <summary>
    /// Wire format for a message published to or read from the inbox queue.
    /// </summary>
    public class MessageEnvelope
    {
        /// <summary>
        /// The message's type, as "Namespace.ClassName, AssemblyName" (no version, no public key) -
        /// enough for <see cref="Type.GetType(string)"/> to resolve it in the reading module's
        /// process, without pinning readers to the exact assembly version the writer used.
        /// </summary>
        public required string MessageType { get; set; }

        /// <summary>The message, serialized on its own.</summary>
        public required string Payload { get; set; }
    }
}