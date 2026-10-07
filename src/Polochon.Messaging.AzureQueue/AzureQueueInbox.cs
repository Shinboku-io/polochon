using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Messaging.AzureQueue.Proxy;

namespace Polochon.Messaging.AzureQueue
{
    /// <summary>
    /// Azure Storage Queue-backed <see cref="IInbox"/>. Also implements <see cref="IErrorQueue"/>:
    /// both a message this inbox could not even read (unknown type, invalid payload) and one that
    /// was read fine but whose processing then failed (see <c>InboxProcessor</c>) end up in the
    /// same <see cref="PersistentInboxOptions.ErrorQueueName"/> queue, so there is a single place
    /// to look for anything this module's inbox could not handle.
    /// </summary>
    public class AzureQueueInbox : IInbox, IErrorQueue
    {
        private readonly IQueueClientProxyFactory queueClientProxy;
        private readonly PersistentInboxOptions options;
        private readonly ILogger<AzureQueueInbox> logger;
        private readonly JsonSerializerOptions internalJsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureQueueInbox"/> class.
        /// </summary>
        /// <param name="queueClientProxy">Creates the proxy used to talk to the inbox and error queues.</param>
        /// <param name="options">The queue endpoint and names this inbox reads from and writes to.</param>
        /// <param name="logger">Logger, resolved through the standard Microsoft.Extensions.Logging pipeline.</param>
        public AzureQueueInbox(IQueueClientProxyFactory queueClientProxy, PersistentInboxOptions options, ILogger<AzureQueueInbox> logger)
        {
            this.queueClientProxy = queueClientProxy;
            this.options = options;
            this.logger = logger;

            internalJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower };
            internalJsonOptions.Converters.Add(new JsonStringDateTimeConverter());
        }

        /// <summary>Gets a proxy for this inbox's own queue.</summary>
        protected IQueueClientProxy GetClientProxy()
            => queueClientProxy.GetProxy(options.QueueEndpoint, options.InboxQueueName);

        /// <summary>Gets a proxy for this inbox's error queue.</summary>
        protected IQueueClientProxy GetErrorClientProxy()
            => queueClientProxy.GetProxy(options.QueueEndpoint, options.ErrorQueueName);

        /// <summary>
        /// Deserializes a raw queue message into the integration event it carries, or <c>null</c>
        /// if its envelope is malformed or its message type is unknown. Does not throw on a
        /// malformed payload; a genuinely unexpected failure (e.g. the type is known but its shape
        /// changed incompatibly) is left to propagate to the caller, which routes it to the error
        /// queue via <see cref="TryDeserializeAsync"/>.
        /// </summary>
        /// <param name="message">The raw queue message to deserialize.</param>
        /// <returns>The deserialized integration event, or <c>null</c> if it could not be recognized.</returns>
        protected virtual IIntegrationEvent? Deserialize(QueueMessage message)
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(message.Body.ToString(), internalJsonOptions);
            if (envelope == null)
            {
                // Invalid message
                return default;
            }

            var queryType = Type.GetType(envelope.MessageType);
            if (queryType == null)
            {
                // Unknown type
                return default;
            }

            return JsonSerializer.Deserialize(envelope.Payload, queryType, internalJsonOptions) as IIntegrationEvent;
        }

        /// <summary>Builds the wire envelope (type + payload) for <paramref name="message"/>.</summary>
        /// <param name="message">The integration event to envelope.</param>
        /// <returns>The envelope, ready to serialize.</returns>
        protected virtual MessageEnvelope CreateEnvelope(IIntegrationEvent message)
        {
            var messageType = message.GetType();

            // Format: "Namespace.ClassName, AssemblyName" (no version, no public key).
            var type = $"{messageType.FullName}, {messageType.Assembly.GetName().Name}";
            var payload = JsonSerializer.Serialize(message, messageType, internalJsonOptions);

            return new MessageEnvelope { MessageType = type, Payload = payload };
        }

        /// <summary>Serializes <paramref name="message"/> to its wire representation.</summary>
        /// <param name="message">The integration event to serialize.</param>
        /// <returns>The serialized envelope.</returns>
        protected virtual string Serialize(IIntegrationEvent message)
            => JsonSerializer.Serialize(CreateEnvelope(message), internalJsonOptions);

        /// <inheritdoc/>
        public async ValueTask PublishMessageAsync(IIntegrationEvent message, CancellationToken cancellationToken)
        {
            var queueClient = GetClientProxy();
            var messageText = Serialize(message);
            _ = await queueClient.SendMessageAsync(messageText, cancellationToken);
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IIntegrationEvent> ReadAllMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var queueClient = GetClientProxy();
            var rawMessages = await queueClient.ReceiveMessagesAsync(maxMessages: 8, visibilityTimeout: new TimeSpan(0, 1, 0), cancellationToken: cancellationToken);

            while (rawMessages.Value.Length != 0 && !cancellationToken.IsCancellationRequested)
            {
                foreach (var message in rawMessages.Value)
                {
                    var readContent = await TryDeserializeAsync(message, cancellationToken);
                    if (readContent != null)
                    {
                        yield return readContent;
                    }

                    _ = await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                }

                rawMessages = await queueClient.ReceiveMessagesAsync(maxMessages: 32, visibilityTimeout: null, cancellationToken: cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async ValueTask PublishFailedMessageAsync(IIntegrationEvent message, Exception exception, CancellationToken cancellationToken)
        {
            var envelope = CreateEnvelope(message);
            await SendToErrorQueueAsync(envelope.MessageType, envelope.Payload, exception, cancellationToken);
        }

        /// <summary>
        /// Deserializes a raw queue message, routing it to the error queue instead of throwing if
        /// it is malformed or unrecognized, or if deserialization otherwise fails unexpectedly.
        /// A message deleted from the inbox queue without ever surfacing here (silently) is
        /// exactly the failure mode this exists to close.
        /// </summary>
        private async ValueTask<IIntegrationEvent?> TryDeserializeAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            try
            {
                var result = Deserialize(message);
                if (result is null)
                {
                    await SendToErrorQueueAsync(
                        messageType: "unknown",
                        payload: message.Body.ToString(),
                        exception: new InvalidOperationException($"Inbox message '{message.MessageId}' has an unrecognized or invalid envelope."),
                        cancellationToken);
                }

                return result;
            }
            catch (Exception ex)
            {
                await SendToErrorQueueAsync(messageType: "unknown", payload: message.Body.ToString(), ex, cancellationToken);
                return null;
            }
        }

        private async ValueTask SendToErrorQueueAsync(string messageType, string payload, Exception exception, CancellationToken cancellationToken)
        {
            try
            {
                var errorEnvelope = new ErrorMessageEnvelope
                {
                    MessageType = messageType,
                    Payload = payload,
                    Error = exception.ToString(),
                };

                var text = JsonSerializer.Serialize(errorEnvelope, internalJsonOptions);
                var errorQueueClient = GetErrorClientProxy();
                _ = await errorQueueClient.SendMessageAsync(text, cancellationToken);
            }
            catch (Exception errorQueueEx)
            {
                // The error queue is the last line of defence - if it also fails, there is
                // nowhere left to route the message but the log, so it does not silently vanish.
                logger.LogError(errorQueueEx, "Error queue itself failed for message type {MessageType} in queue {InboxQueueName}", messageType, options.InboxQueueName);
            }
        }
    }
}
