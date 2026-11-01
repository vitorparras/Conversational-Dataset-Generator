using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>
/// Every column of the content schema, in the list of those stored in clear and the list of those stored sealed. A column in neither list is a
/// defect, and the check of the schema at startup refuses it; a column enters the second list the moment it holds text that is not pseudonymized.
/// </summary>
public static class ContentColumns
{
    /// <summary>The table of the texts of an item.</summary>
    public static string ItemText { get; } = "item_text";

    /// <summary>The table of the maps that reverse a pseudonym.</summary>
    public static string ReversalMap { get; } = "reversal_map";

    /// <summary>The text as it arrived.</summary>
    public static ProtectedColumn RawText { get; } = new(ItemText, "raw_text", LabelPurpose.RawText);

    /// <summary>The text after sanitization, which has the key and the authorization of the raw text.</summary>
    public static ProtectedColumn SanitizedText { get; } = new(ItemText, "sanitized_text", LabelPurpose.RawText);

    /// <summary>What each pseudonym replaced.</summary>
    public static ProtectedColumn Entries { get; } = new(ReversalMap, "entries", LabelPurpose.ReversalMap);

    /// <summary>The columns stored sealed.</summary>
    public static IReadOnlyList<ProtectedColumn> Protected { get; } = [RawText, SanitizedText, Entries];

    /// <summary>The columns stored in clear: identifiers, the domain, dates and the pseudonymized text.</summary>
    public static IReadOnlyList<ClearColumn> Clear { get; } =
    [
        new(ItemText, "id", ClearKind.Identifier),
        new(ItemText, "domain_id", ClearKind.Domain),
        new(ItemText, "recorded_at", ClearKind.Moment),
        new(ItemText, "emptied_at", ClearKind.Moment),
        new(ItemText, "pseudonymized_text", ClearKind.PseudonymizedText),
        new(ReversalMap, "id", ClearKind.Identifier),
        new(ReversalMap, "domain_id", ClearKind.Domain),
        new(ReversalMap, "item_id", ClearKind.Identifier),
        new(ReversalMap, "recorded_at", ClearKind.Moment),
        new(ReversalMap, "emptied_at", ClearKind.Moment),
    ];
}
