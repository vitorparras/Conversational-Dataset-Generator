using System.Collections.Frozen;
using System.Reflection;

namespace Core.Persistence.Content;

/// <summary>
/// Every query the system exposes over content, by name, so that a test can run each one with no domain declared and demand that it returns
/// nothing. A query is a <c>public</c> or <c>internal const string</c> field of this class, and the list is read from the fields, so adding one is all it takes
/// to be tested; the architecture rule keeps every statement over content in such a constant of this file.
/// </summary>
public static class ContentQueries
{
    /// <summary>The sealed raw text of an item, and whether the item was emptied.</summary>
    internal const string ReadRawText = "SELECT raw_text, emptied_at IS NOT NULL FROM content.item_text WHERE id = @id";

    /// <summary>The sealed sanitized text of an item, and whether the item was emptied.</summary>
    internal const string ReadSanitizedText = "SELECT sanitized_text, emptied_at IS NOT NULL FROM content.item_text WHERE id = @id";

    /// <summary>The sealed map of a reversal, and whether it was emptied.</summary>
    internal const string ReadReversalEntries = "SELECT entries, emptied_at IS NOT NULL FROM content.reversal_map WHERE id = @id";

    internal const string ReadFirstRawText = "SELECT id, raw_text FROM content.item_text WHERE emptied_at IS NULL ORDER BY id LIMIT 1";

    internal const string ReadFirstEntries = "SELECT id, entries FROM content.reversal_map WHERE emptied_at IS NULL ORDER BY id LIMIT 1";

    /// <summary>Adds an item; the texts that are sealed arrive sealed.</summary>
    internal const string InsertItemText =
        "INSERT INTO content.item_text (domain_id, raw_text, sanitized_text, pseudonymized_text) VALUES (@domain, @raw, @sanitized, @pseudonymized) RETURNING id";

    /// <summary>Adds the map that reverses the pseudonyms of an item.</summary>
    internal const string InsertReversalMap = "INSERT INTO content.reversal_map (domain_id, item_id, entries) VALUES (@domain, @item, @entries) RETURNING id";

    /// <summary>The row of an item of the domain that was emptied, none when it was not; asked of the table of the items emptied, so that the audit sees no read of a text.</summary>
    internal const string ReadItemEmptied = "SELECT true FROM content.item_emptied WHERE item_id = @id";

    /// <summary>Empties an item: the marker is set and every column of content is cleared, in one statement; an item emptied already is left as it is.</summary>
    internal const string EmptyItemText =
        "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL WHERE id = @id AND emptied_at IS NULL RETURNING id";

    /// <summary>Empties the maps of an item, with the item: the marker is set and the entries are cleared.</summary>
    internal const string EmptyReversalMaps = "UPDATE content.reversal_map SET emptied_at = now(), entries = NULL WHERE item_id = @id AND emptied_at IS NULL";

    /// <summary>The queries over content, as the statement each one runs, by the name of its constant.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = Declared(typeof(ContentQueries));

    /// <summary>The public and internal string constants the type declares, by name; a public constant is refused by the analyzer, so the queries are internal.</summary>
    internal static FrozenDictionary<string, string> Declared(Type type) =>
        type.GetRuntimeFields()
            .Where(field => field.DeclaringType == type && field.IsLiteral && !field.IsPrivate && field.FieldType == typeof(string))
            .ToFrozenDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
}
