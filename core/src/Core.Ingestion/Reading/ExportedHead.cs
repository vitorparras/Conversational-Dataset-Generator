using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>Recognises the format of the exported conversation by its first bytes: UTF-8 without a mark, a JSON object, and the member <c>formato</c> naming the format.</summary>
internal static class ExportedHead
{
    private const string FormatMember = "formato";

    /// <summary>
    /// Whether the first bytes hold an object whose top-level member <c>formato</c> is the name given, found before the head ends;
    /// a byte order mark, another encoding or a name that differs says no.
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
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals(FormatMember))
            {
                return reader.Read() && reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(format);
            }

            if (!reader.Read() || !reader.TrySkip())
            {
                return false;
            }
        }

        return false;
    }
}
