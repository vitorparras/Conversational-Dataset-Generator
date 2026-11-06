using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>
/// The structure of the exported conversation points at nothing outside the file: an identifier, a sender or an answered message that reads as an address or a path
/// is turned back, never followed.
/// </summary>
internal static class ExternalReferences
{
    private const char Backslash = '\\';
    private const int DriveLength = 3;

    /// <summary>Whether a structure member of some message reads as an address or a path; the text of a message is content, and is not looked at.</summary>
    internal static bool Found(JsonElement messages)
    {
        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind == JsonValueKind.Object && (Points(message, "id") || Points(message, "remetente") || Points(message, "responde_a")))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Points(JsonElement message, string member) =>
        message.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && Reads(text);

    /// <summary>An address with a scheme, a step up to the folder above, a root, or a drive letter, at the places of the value where each one means it.</summary>
    private static bool Reads(string text) => HasScheme(text) || StepsUp(text) || Rooted(text) || Drive(text);

    private static bool HasScheme(string text) => text.Contains("://", StringComparison.Ordinal);

    private static bool StepsUp(string text) => text.Contains("../", StringComparison.Ordinal) || text.Contains("..\\", StringComparison.Ordinal);

    private static bool Rooted(string text) => text.StartsWith('/') || text.StartsWith(Backslash) || text.StartsWith("~/", StringComparison.Ordinal);

    private static bool Drive(string text) => text.Length >= DriveLength && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[DriveLength - 1] is '/' or Backslash;
}
