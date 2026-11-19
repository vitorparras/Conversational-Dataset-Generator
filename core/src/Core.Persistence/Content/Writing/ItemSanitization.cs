namespace Core.Persistence.Content.Writing;

/// <summary>
/// What the sanitization left with an item: the sanitized text and the trail, sealed like the raw text, and in clear the version of the list of steps, the list itself and how many edits each step
/// made. The database checks that the version is the fingerprint of the list and that there is one count for each step, so a version cannot name a list it does not hash.
/// </summary>
public sealed record ItemSanitization
{
    /// <summary>The sanitized text; sealed on its way to the database.</summary>
    public required string Text { get; init; }

    /// <summary>The version of the list of steps: the SHA-256, in lowercase hexadecimal, of the list.</summary>
    public required string Version { get; init; }

    /// <summary>The steps in the order they ran, one name@version per line; written once per domain with its version.</summary>
    public required string Steps { get; init; }

    /// <summary>The trail that translates the positions of the sanitized text back to the raw one; sealed on its way to the database.</summary>
    public required string Trail { get; init; }

    /// <summary>How many edits each step made, in the order of the steps.</summary>
    public required IReadOnlyList<int> Counts { get; init; }

    /// <summary>The version and the counts; never the text nor the trail.</summary>
    public override string ToString() => $"ItemSanitization {{ Version = {Version}, Counts = {string.Join(", ", Counts)} }}";
}
