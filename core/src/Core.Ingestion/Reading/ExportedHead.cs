using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>Recognises the format of the exported conversation by its first bytes: UTF-8 without a mark, a JSON object, and the member <c>formato</c> naming the format, first of all.</summary>
internal static class ExportedHead
{
    private const string FormatMember = "formato";

    /// <summary>
    /// Whether the first bytes hold an object whose first member is <c>formato</c>, naming the format given; the position does not depend on the size of the file.
    /// A byte order mark, another encoding, another first member or a name that differs says no.
    /// </summary>
    internal static bool Confirms(ReadOnlySpan<byte> head, string format)
    {
        var reader = new Utf8JsonReader(head, isFinalBlock: false, default);
        try
        {
            return reader.Read() && reader.TokenType == JsonTokenType.StartObject && NamesTheFormat(ref reader, format);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool NamesTheFormat(ref Utf8JsonReader reader, string format)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || !reader.ValueTextEquals(FormatMember))
        {
            return false;
        }

        return reader.Read() && reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(format);
    }
}
