namespace Core.Ingestion.Reading;

/// <summary>How deep a JSON text nests, counted without building anything: a text that goes too deep is turned back before a parser is asked to follow it.</summary>
internal static class JsonNesting
{
    private const byte Backslash = (byte)'\\';
    private const byte Quote = (byte)'"';

    /// <summary>Whether the text opens more levels at once than <paramref name="maxDepth" />; what is inside a string is not structure.</summary>
    internal static bool Exceeds(ReadOnlySpan<byte> json, int maxDepth)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        foreach (var current in json)
        {
            if (inString)
            {
                (inString, escaped) = Inside(current, escaped);
                continue;
            }

            inString = current == Quote;
            depth += Change(current);
            if (depth > maxDepth)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the string goes on after the byte, and whether the byte that follows is escaped.</summary>
    private static (bool InString, bool Escaped) Inside(byte current, bool escaped)
    {
        if (escaped)
        {
            return (true, false);
        }

        return (current != Quote, current == Backslash);
    }

    private static int Change(byte current) => current switch
    {
        (byte)'{' or (byte)'[' => 1,
        (byte)'}' or (byte)']' => -1,
        _ => 0,
    };
}
