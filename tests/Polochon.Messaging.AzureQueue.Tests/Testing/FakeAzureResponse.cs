using Azure;
using Azure.Core;

namespace Polochon.Messaging.AzureQueue.Tests.Testing
{
    /// <summary>
    /// Minimal <see cref="Response"/> used to build fake <see cref="Response{T}"/> results for
    /// <see cref="FakeQueueClientProxy"/> - nothing in these tests inspects the raw HTTP response,
    /// only the deserialized value it wraps.
    /// </summary>
    internal sealed class FakeAzureResponse : Response
    {
        public override int Status => 200;

        public override string ReasonPhrase => "OK";

        public override Stream? ContentStream { get; set; }

        public override string ClientRequestId { get; set; } = string.Empty;

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, out string value)
        {
            value = string.Empty;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = [];
            return false;
        }
    }
}
