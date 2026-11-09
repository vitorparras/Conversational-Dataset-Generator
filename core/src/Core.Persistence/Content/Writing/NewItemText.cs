namespace Core.Persistence.Content.Writing;

/// <summary>
/// The texts of an item as they are when it is added: the raw text always, the others once the stage that makes them has run, and always what was decided for each occurrence found.
/// Only the host builds one, from the material the destinations were decided for, so that no caller records an item as clean without having analysed its text.
/// </summary>
public sealed record NewItemText
{
    /// <summary>The texts of an item with the occurrences found in the raw text and the destination of each.</summary>
    /// <param name="raw">The text as it arrived; sealed on its way to the database.</param>
    /// <param name="sanitized">The sanitized text, sealed like the raw one; null while it does not exist.</param>
    /// <param name="pseudonymized">The pseudonymized text, the one text stored in clear; null while it does not exist.</param>
    /// <param name="occurrences">The occurrences with their destination, sealed with the item and never changed; an empty list is the decision that none was found.</param>
    internal NewItemText(string raw, string? sanitized, string? pseudonymized, IReadOnlyList<ItemOccurrence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(occurrences);
        Raw = raw;
        Sanitized = sanitized;
        Pseudonymized = pseudonymized;
        Occurrences = occurrences;
    }

    /// <summary>The text as it arrived; sealed on its way to the database.</summary>
    public string Raw { get; }

    /// <summary>The sanitized text, sealed like the raw one; null while it does not exist.</summary>
    public string? Sanitized { get; }

    /// <summary>The pseudonymized text, the one text stored in clear; null while it does not exist.</summary>
    public string? Pseudonymized { get; }

    /// <summary>The occurrences found in the raw text and the destination of each, sealed with the item and never changed.</summary>
    public IReadOnlyList<ItemOccurrence> Occurrences { get; }
}
