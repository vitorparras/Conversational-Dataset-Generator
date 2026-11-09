namespace Core.Persistence.Settings;

/// <summary>
/// A change of configuration as the screen submits it (decision 0053): the key, the new value and the code of the motive, by a person. The shape is the database's:
/// a key of a section and a name, a value of up to 256 printable ASCII characters and a motive in snake_case; which keys and motives the screen offers is not decided here.
/// </summary>
public sealed record ConfigurationSubmission
{
    private const int LongestName = 63;
    private const int LongestValue = 256;
    private const int LongestMotive = 32;

    /// <summary>The key, as <c>Section:Name</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The new value, never a secret.</summary>
    public required string Value { get; init; }

    /// <summary>The code of the motive.</summary>
    public required string Motive { get; init; }

    /// <summary>The person who submits.</summary>
    public required long AuthorId { get; init; }

    /// <summary>Whether the database takes the submission as it is.</summary>
    public bool HasShape => IsKey(Key) && IsValue(Value) && IsMotive(Motive) && AuthorId > 0;

    /// <summary>A section and a name, each an uppercase letter and then letters, up to 63 in all, joined by a colon.</summary>
    private static bool IsKey(string key)
    {
        var parts = key.Split(':');
        return parts.Length == 2 && IsName(parts[0]) && IsName(parts[1]);
    }

    private static bool IsName(string name) => name.Length is >= 1 and <= LongestName && char.IsAsciiLetterUpper(name[0]) && name.All(char.IsAsciiLetter);

    /// <summary>From 1 to 256 printable ASCII characters, the space included and no control character.</summary>
    private static bool IsValue(string value) => value.Length is >= 1 and <= LongestValue && value.All(character => character is >= ' ' and <= '~');

    /// <summary>A lowercase letter and then lowercase letters and underscores, up to 32 in all.</summary>
    private static bool IsMotive(string motive) =>
        motive.Length is >= 1 and <= LongestMotive && char.IsAsciiLetterLower(motive[0]) && motive.All(character => char.IsAsciiLetterLower(character) || character == '_');
}
