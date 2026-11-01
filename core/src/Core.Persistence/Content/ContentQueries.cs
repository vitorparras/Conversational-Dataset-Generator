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
    /// <summary>The sealed raw text of an item.</summary>
    internal const string ReadRawText = "SELECT raw_text FROM content.item_text WHERE id = @id";

    /// <summary>The sealed sanitized text of an item.</summary>
    internal const string ReadSanitizedText = "SELECT sanitized_text FROM content.item_text WHERE id = @id";

    /// <summary>The sealed map of a reversal.</summary>
    internal const string ReadReversalEntries = "SELECT entries FROM content.reversal_map WHERE id = @id";

    /// <summary>Adds an item; the texts that are sealed arrive sealed.</summary>
    internal const string InsertItemText =
        "INSERT INTO content.item_text (domain_id, raw_text, sanitized_text, pseudonymized_text) VALUES (@domain, @raw, @sanitized, @pseudonymized) RETURNING id";

    /// <summary>Adds the map that reverses the pseudonyms of an item.</summary>
    internal const string InsertReversalMap = "INSERT INTO content.reversal_map (domain_id, item_id, entries) VALUES (@domain, @item, @entries) RETURNING id";

    /// <summary>The queries over content, as the statement each one runs, by the name of its constant.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = Declared(typeof(ContentQueries));

    /// <summary>The public and internal string constants the type declares, by name; a public constant is refused by the analyzer, so the queries are internal.</summary>
    internal static FrozenDictionary<string, string> Declared(Type type) =>
        type.GetRuntimeFields()
            .Where(field => field.DeclaringType == type && field.IsLiteral && !field.IsPrivate && field.FieldType == typeof(string))
            .ToFrozenDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
}
