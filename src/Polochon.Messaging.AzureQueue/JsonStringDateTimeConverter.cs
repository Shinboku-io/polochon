using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Polochon.Messaging.AzureQueue
{
    /// <summary>
    /// Converts <see cref="DateTimeOffset"/> to and from JSON string representation.
    /// </summary>
    public class JsonStringDateTimeConverter : JsonConverter<DateTimeOffset>
    {
        /// <summary>
        /// Reads and converts the JSON string representation of a date and time to a <see cref="DateTimeOffset"/> object.
        /// </summary>
        /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <returns>The converted <see cref="DateTimeOffset"/> object.</returns>
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString() ?? string.Empty, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes a <see cref="DateTimeOffset"/> object as a JSON string representation.
        /// </summary>
        /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
        /// <param name="value">The <see cref="DateTimeOffset"/> value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffK", CultureInfo.InvariantCulture));
        }
    }
}