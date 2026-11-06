using System.Text.Json;
using System.Text.RegularExpressions;

namespace Core.Ingestion.Reading;

/// <summary>
/// The structure of the exported conversation points at nothing outside the file: an identifier, a sender or an answered message that reads as an address or a path
/// is turned back, never followed.
/// </summary>
internal static partial class ExternalReferences
{
    private static readonly string[] Members = ["id", "remetente", "responde_a"];

    /// <summary>Whether a structure member of some message reads as an address or a path; the text of a message is content, and is not looked at.</summary>
    internal static bool Found(JsonElement messages)
    {
        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind == JsonValueKind.Object && Array.Exists(Members, name => message.TryGetProperty(name, out var value) && LooksLikeOne(value)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeOne(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { } text && Reference().IsMatch(text);

    /// <summary>An address with a scheme, a step up to the folder above, a root, or a drive letter, at the places of the value where each one means it.</summary>
    [GeneratedRegex(@"://|\.\.[/\\]|^[/\\]|^~/|^[A-Za-z]:[/\\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference();
}
