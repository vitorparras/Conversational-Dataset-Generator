using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>Tells a JSON that ends before it is done from one that is wrong: the reader that is not told it has all the bytes runs out of them and stops, where the wrong one throws.</summary>
internal static class JsonEnds
{
    /// <summary>True when the bytes read as JSON up to their end, with no error, and the document was not finished.</summary>
    /// <param name="content">The bytes of the content.</param>
    internal static bool EndsBeforeTheContentDoes(ReadOnlySpan<byte> content)
    {
        var reader = new Utf8JsonReader(content, isFinalBlock: false, default);
        try
        {
            bool more;
            do
            {
                more = reader.Read();
            }
            while (more);
        }
        catch (JsonException)
        {
            return false;
        }

        return reader.CurrentDepth > 0 || reader.TokenType == JsonTokenType.None;
    }
}
