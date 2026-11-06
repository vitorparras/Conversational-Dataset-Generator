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
    /// <summary>The sealed raw text of an item, whether the item was emptied, and the key of the item wrapped with its epoch.</summary>
    internal const string ReadRawText = "SELECT raw_text, emptied_at IS NOT NULL, dek_epoch, dek_wrapped FROM content.item_text WHERE id = @id";

    /// <summary>The sealed sanitized text of an item, whether the item was emptied, and the key of the item wrapped with its epoch.</summary>
    internal const string ReadSanitizedText = "SELECT sanitized_text, emptied_at IS NOT NULL, dek_epoch, dek_wrapped FROM content.item_text WHERE id = @id";

    /// <summary>The sealed map of a reversal, whether it was emptied, and the key of the map wrapped with its epoch.</summary>
    internal const string ReadReversalEntries = "SELECT entries, emptied_at IS NOT NULL, dek_epoch, dek_wrapped FROM content.reversal_map WHERE id = @id";

    internal const string ReadFirstRawText = "SELECT id, raw_text, dek_epoch, dek_wrapped FROM content.item_text WHERE emptied_at IS NULL AND id > @id ORDER BY id LIMIT 1";

    internal const string ReadFirstEntries = "SELECT id, entries, dek_epoch, dek_wrapped FROM content.reversal_map WHERE emptied_at IS NULL AND id > @id ORDER BY id LIMIT 1";

    /// <summary>The rows of the copy of the data, as the database writes them, for the domain declared: the sealed columns go as the bytes they are, never opened.</summary>
    /// <summary>Opens a batch of ingestion for the domain declared: it holds no content, and the state, the moments and the identifier are the database's.</summary>
    internal const string InsertIngestionBatch = "INSERT INTO content.ingestion_batch (domain_id, opened_by) VALUES (@domain, @author) RETURNING id";

    /// <summary>The state and the motive of a batch of the domain, whatever its state: a batch rejected stays consultable after its material is gone.</summary>
    internal const string ReadIngestionBatch = "SELECT state, motive, opened_by FROM content.ingestion_batch WHERE id = @id";

    /// <summary>Closes a batch that is open, accepted or rejected with its motive; the database refuses it without the event of the closing in the same transaction.</summary>
    internal const string CloseIngestionBatch = "UPDATE content.ingestion_batch SET state = @state, motive = @motive WHERE id = @id AND state = 'open'";

    internal const string ReadCopyIngestionBatch = "SELECT pg_catalog.to_jsonb(t)::text FROM content.ingestion_batch t ORDER BY id";

    internal const string ReadCopyItemText = "SELECT pg_catalog.to_jsonb(t)::text FROM content.item_text t ORDER BY id";

    internal const string ReadCopyReversalMap = "SELECT pg_catalog.to_jsonb(t)::text FROM content.reversal_map t ORDER BY id";

    internal const string ReadCopyItemEmptied = "SELECT pg_catalog.to_jsonb(t)::text FROM content.item_emptied t ORDER BY id";

    /// <summary>Adds an item; the texts that are sealed arrive sealed, with the key of the item wrapped.</summary>
    internal const string InsertItemText =
        "INSERT INTO content.item_text (domain_id, raw_text, sanitized_text, pseudonymized_text, dek_epoch, dek_wrapped) "
        + "VALUES (@domain, @raw, @sanitized, @pseudonymized, @epoch, @wrapped) RETURNING id";

    /// <summary>Adds the map that reverses the pseudonyms of an item.</summary>
    internal const string InsertReversalMap =
        "INSERT INTO content.reversal_map (domain_id, item_id, entries, dek_epoch, dek_wrapped) VALUES (@domain, @item, @entries, @epoch, @wrapped) RETURNING id";

    /// <summary>The row of an item of the domain that was emptied, none when it was not; asked of the table of the items emptied, so that the audit sees no read of a text.</summary>
    internal const string ReadItemEmptied = "SELECT true FROM content.item_emptied WHERE item_id = @id";

    /// <summary>
    /// Empties an item: the marker is set and every column of content is cleared, the key of the item with them, in one statement; an item emptied already is left as it is.
    /// </summary>
    internal const string EmptyItemText =
        "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL, dek_epoch = NULL, dek_wrapped = NULL "
        + "WHERE id = @id AND emptied_at IS NULL RETURNING id";

    /// <summary>Empties the maps of an item, with the item: the marker is set and the entries and the key of the map are cleared.</summary>
    internal const string EmptyReversalMaps = "UPDATE content.reversal_map SET emptied_at = now(), entries = NULL, dek_epoch = NULL, dek_wrapped = NULL WHERE item_id = @id AND emptied_at IS NULL";

    /// <summary>The keys, wrapped, that an epoch older than the newest wraps, among the items not emptied; a page of them.</summary>
    internal const string ReadOlderItemKeys =
        "SELECT id, dek_epoch, dek_wrapped FROM content.item_text WHERE emptied_at IS NULL AND dek_epoch < @newest ORDER BY id LIMIT 500";

    /// <summary>The keys, wrapped, that an epoch older than the newest wraps, among the maps not emptied; a page of them.</summary>
    internal const string ReadOlderMapKeys =
        "SELECT id, dek_epoch, dek_wrapped FROM content.reversal_map WHERE emptied_at IS NULL AND dek_epoch < @newest ORDER BY id LIMIT 500";

    internal const string ReadItemKeysAfter = "SELECT id, dek_epoch, dek_wrapped FROM content.item_text WHERE emptied_at IS NULL AND id > @after ORDER BY id LIMIT @batch";

    internal const string ReadMapKeysAfter = "SELECT id, dek_epoch, dek_wrapped FROM content.reversal_map WHERE emptied_at IS NULL AND id > @after ORDER BY id LIMIT @batch";

    /// <summary>The sealed raw texts of three items not emptied, each with the key of the item wrapped with its epoch: the sample that the rehearsal of the recovery opens.</summary>
    internal const string ReadRawSamples = "SELECT raw_text, dek_epoch, dek_wrapped FROM content.item_text WHERE emptied_at IS NULL ORDER BY id LIMIT 3";

    /// <summary>The sealed maps of three reversals not emptied, each with the key of the map wrapped with its epoch: the sample that the rehearsal of the recovery opens.</summary>
    internal const string ReadMapSamples = "SELECT entries, dek_epoch, dek_wrapped FROM content.reversal_map WHERE emptied_at IS NULL ORDER BY id LIMIT 3";

    /// <summary>Wraps again the key of an item not emptied; only the wrapped key and its epoch change.</summary>
    internal const string RewrapItemKey = "UPDATE content.item_text SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id AND emptied_at IS NULL";

    /// <summary>Wraps again the key of a map not emptied; only the wrapped key and its epoch change.</summary>
    internal const string RewrapMapKey = "UPDATE content.reversal_map SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id AND emptied_at IS NULL";

    /// <summary>The epochs that the keys of the rows not emptied name, in the domain declared.</summary>
    internal const string ReadEpochsInUse =
        "SELECT dek_epoch FROM content.item_text WHERE emptied_at IS NULL UNION SELECT dek_epoch FROM content.reversal_map WHERE emptied_at IS NULL";

    /// <summary>The queries over content, as the statement each one runs, by the name of its constant.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = Declared(typeof(ContentQueries));

    /// <summary>The public and internal string constants the type declares, by name; a public constant is refused by the analyzer, so the queries are internal.</summary>
    internal static FrozenDictionary<string, string> Declared(Type type) =>
        type.GetRuntimeFields()
            .Where(field => field.DeclaringType == type && field.IsLiteral && !field.IsPrivate && field.FieldType == typeof(string))
            .ToFrozenDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
}
