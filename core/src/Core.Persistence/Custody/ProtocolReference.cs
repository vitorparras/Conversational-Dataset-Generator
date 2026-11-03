namespace Core.Persistence.Custody;

/// <summary>
/// The reference of an event to a protocol outside the system: a number, in letters, digits and the signs . _ / -, up to 64 characters,
/// so that no name and no personal data fits in it.
/// </summary>
internal static class ProtocolReference
{
    internal const int MaxLength = 64;

    internal static bool IsValid(string? reference) =>
        reference is { Length: > 0 and <= MaxLength } && reference.All(IsAllowed);

    private static bool IsAllowed(char character) => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '/' or '-';
}
