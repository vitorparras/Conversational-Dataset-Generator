using Core.Persistence.Content.Writing;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>The texts of an item for the tests that are not about the occurrences: the decision is explicit, and it is that no occurrence was found.</summary>
public static class ItemTexts
{
    /// <summary>The texts of an item, with an empty list of occurrences as the decision.</summary>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="sanitized">The sanitized text; null while it does not exist.</param>
    /// <param name="pseudonymized">The pseudonymized text; null while it does not exist.</param>
    public static NewItemText Of(string raw, string? sanitized = null, string? pseudonymized = null) => new(raw, sanitized, pseudonymized, []);
}
