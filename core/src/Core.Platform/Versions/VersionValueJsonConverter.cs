using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Platform.Versions;

/// <summary>Writes a present version as a JSON string and an absent one as <c>null</c>, so the reader cannot take one for the other.</summary>
internal sealed class VersionValueJsonConverter : JsonConverter<VersionValue>
{
    public override VersionValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("A version is reported by the system, never read back from JSON.");

    public override void Write(Utf8JsonWriter writer, VersionValue value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Text is { } text)
        {
            writer.WriteStringValue(text);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
