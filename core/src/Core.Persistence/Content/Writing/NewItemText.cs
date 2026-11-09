namespace Core.Persistence.Content.Writing;

/// <summary>The texts of an item as they are when it is added: the raw text always, the others once the stage that makes them has run.</summary>
/// <param name="Raw">The text as it arrived; sealed on its way to the database.</param>
/// <param name="Sanitized">The sanitized text, sealed like the raw one; null while it does not exist.</param>
/// <param name="Pseudonymized">The pseudonymized text, the one text stored in clear; null while it does not exist.</param>
/// <param name="Occurrences">
/// The occurrences found in the raw text and the destination of each, sealed with the item and never changed; an empty list says the text was analysed and nothing was found,
/// and null says no decision was made, which is not the same.
/// </param>
public sealed record NewItemText(string Raw, string? Sanitized = null, string? Pseudonymized = null, IReadOnlyList<ItemOccurrence>? Occurrences = null);
