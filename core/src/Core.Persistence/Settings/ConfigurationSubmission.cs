using System.Text.RegularExpressions;

namespace Core.Persistence.Settings;

/// <summary>
/// A change of configuration as the screen submits it (decision 0053): the key, the new value and the code of the motive, by a person. The shape is the database's:
/// a key of a section and a name, a value of up to 256 printable ASCII characters and a motive in snake_case; which keys and motives the screen offers is not decided here.
/// </summary>
public sealed partial record ConfigurationSubmission
{
    /// <summary>The key, as <c>Section:Name</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The new value, never a secret.</summary>
    public required string Value { get; init; }

    /// <summary>The code of the motive.</summary>
    public required string Motive { get; init; }

    /// <summary>The person who submits.</summary>
    public required long AuthorId { get; init; }

    /// <summary>Whether the database takes the submission as it is.</summary>
    public bool HasShape => KeyShape().IsMatch(Key) && ValueShape().IsMatch(Value) && MotiveShape().IsMatch(Motive) && AuthorId > 0;

    [GeneratedRegex("^[A-Z][A-Za-z]{0,62}:[A-Z][A-Za-z]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyShape();

    [GeneratedRegex("^[ -~]{1,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueShape();

    [GeneratedRegex("^[a-z][a-z_]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex MotiveShape();
}
