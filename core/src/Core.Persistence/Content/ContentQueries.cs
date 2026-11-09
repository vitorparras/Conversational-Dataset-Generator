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

    /// <summary>The sealed occurrences of an item, whether the item was emptied, and the key of the item wrapped with its epoch.</summary>
    internal const string ReadOccurrences = "SELECT occurrences, emptied_at IS NOT NULL, dek_epoch, dek_wrapped FROM content.item_text WHERE id = @id";

    /// <summary>The sealed map of a reversal, whether it was emptied, and the key of the map wrapped with its epoch.</summary>
    internal const string ReadReversalEntries = "SELECT entries, emptied_at IS NOT NULL, dek_epoch, dek_wrapped FROM content.reversal_map WHERE id = @id";

    internal const string ReadFirstRawText = "SELECT id, raw_text, dek_epoch, dek_wrapped FROM content.item_text WHERE emptied_at IS NULL AND id > @id ORDER BY id LIMIT 1";

    internal const string ReadFirstEntries = "SELECT id, entries, dek_epoch, dek_wrapped FROM content.reversal_map WHERE emptied_at IS NULL AND id > @id ORDER BY id LIMIT 1";

    /// <summary>The rows of the copy of the data, as the database writes them, for the domain declared: the sealed columns go as the bytes they are, never opened.</summary>
    internal const string ReadCopyItemText = "SELECT pg_catalog.to_jsonb(t)::text FROM content.item_text t ORDER BY id";

    internal const string ReadCopyReversalMap = "SELECT pg_catalog.to_jsonb(t)::text FROM content.reversal_map t ORDER BY id";

    internal const string ReadCopyItemEmptied = "SELECT pg_catalog.to_jsonb(t)::text FROM content.item_emptied t ORDER BY id";

    /// <summary>Adds an item; the texts that are sealed arrive sealed, with the key of the item wrapped.</summary>
    internal const string InsertItemText =
        "INSERT INTO content.item_text (domain_id, batch_id, raw_text, sanitized_text, pseudonymized_text, occurrences, dek_epoch, dek_wrapped) "
        + "VALUES (@domain, @batch, @raw, @sanitized, @pseudonymized, @occurrences, @epoch, @wrapped) RETURNING id";

    /// <summary>Adds the map that reverses the pseudonyms of an item.</summary>
    internal const string InsertReversalMap =
        "INSERT INTO content.reversal_map (domain_id, batch_id, item_id, entries, dek_epoch, dek_wrapped) VALUES (@domain, @batch, @item, @entries, @epoch, @wrapped) RETURNING id";

    /// <summary>Opens a batch of ingestion for the domain declared, under its condition of origin: it holds no content, and the state, the moments and the identifier are the database's.</summary>
    internal const string InsertIngestionBatch =
        "INSERT INTO content.ingestion_batch (domain_id, opened_by, condition, submission, received_bytes, received_digest, warning, origin_state) "
        + "VALUES (@domain, @author, @condition, @submission, @bytes, @digest, @warning, @origin) RETURNING id";

    /// <summary>Keeps the origin of a batch sealed, with the key of the row wrapped, in the transaction that opens the batch.</summary>
    internal const string InsertBatchOrigin =
        "INSERT INTO content.batch_origin (domain_id, batch_id, origin, dek_epoch, dek_wrapped) VALUES (@domain, @batch, @origin, @epoch, @wrapped) RETURNING id";

    /// <summary>The sealed origin of a batch, never emptied, and the key of the row wrapped with its epoch.</summary>
    internal const string ReadBatchOrigin = "SELECT origin, false, dek_epoch, dek_wrapped FROM content.batch_origin WHERE batch_id = @id";

    /// <summary>The origins of the domain declared, as the database writes them, for the copy of the data.</summary>
    internal const string ReadCopyBatchOrigin = "SELECT pg_catalog.to_jsonb(t)::text FROM content.batch_origin t ORDER BY id";

    /// <summary>The keys, wrapped, that an epoch older than the newest wraps, among the origins; a page of them.</summary>
    internal const string ReadOlderOriginKeys = "SELECT id, dek_epoch, dek_wrapped FROM content.batch_origin WHERE dek_epoch < @newest ORDER BY id LIMIT 500";

    internal const string ReadOriginKeysAfter = "SELECT id, dek_epoch, dek_wrapped FROM content.batch_origin WHERE id > @after ORDER BY id LIMIT @batch";

    /// <summary>Wraps again the key of an origin; only the wrapped key and its epoch change.</summary>
    internal const string RewrapOriginKey = "UPDATE content.batch_origin SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id";

    /// <summary>The state, the motive and the condition of a batch of the domain, whatever its state: a batch rejected stays consultable after its material is gone.</summary>
    internal const string ReadIngestionBatch = "SELECT state, motive, opened_by, condition FROM content.ingestion_batch WHERE id = @id";

    /// <summary>The condition of an item that can be read, from the view that joins the item to its batch.</summary>
    internal const string ReadItemCondition = "SELECT condition FROM public.item_condition WHERE item_id = @id";

    /// <summary>The batches of the domain declared, as the database writes them, for the copy of the data.</summary>
    internal const string ReadCopyIngestionBatch = "SELECT pg_catalog.to_jsonb(t)::text FROM content.ingestion_batch t ORDER BY id";

    /// <summary>The batches of the domain declared, newest first, by state and by the moment of the submission, with what each submission brought.</summary>
    internal const string ReadIngestionBatchPage =
        "SELECT id, state, motive, opened_by, opened_at, submission, received_bytes, received_digest, warning, origin_state FROM content.ingestion_batch "
        + "WHERE (@state::text IS NULL OR state = @state) AND (@from::timestamptz IS NULL OR opened_at >= @from) AND (@until::timestamptz IS NULL OR opened_at < @until) "
        + "ORDER BY opened_at DESC, id DESC LIMIT @limit";

    /// <summary>One batch of the domain declared, as the register shows it.</summary>
    internal const string ReadIngestionBatchEntry =
        "SELECT id, state, motive, opened_by, opened_at, submission, received_bytes, received_digest, warning, origin_state FROM content.ingestion_batch WHERE id = @id";

    /// <summary>Closes a batch that is open, accepted or rejected with its motive; the database refuses it without the event of the closing in the same transaction.</summary>
    internal const string CloseIngestionBatch = "UPDATE content.ingestion_batch SET state = @state, motive = @motive WHERE id = @id AND state = 'open'";

    /// <summary>Removes the maps of a batch just rejected, before its items; the database refuses it for any batch that is not rejected in this transaction.</summary>
    internal const string RemoveBatchMaps = "DELETE FROM content.reversal_map WHERE batch_id = @id";

    /// <summary>Removes the items of a batch just rejected, after its maps; the database refuses it for any batch that is not rejected in this transaction.</summary>
    internal const string RemoveBatchItems = "DELETE FROM content.item_text WHERE batch_id = @id";

    /// <summary>The row of an item of the domain that was emptied, none when it was not; asked of the table of the items emptied, so that the audit sees no read of a text.</summary>
    internal const string ReadItemEmptied = "SELECT true FROM content.item_emptied WHERE item_id = @id";

    /// <summary>
    /// Empties an item: the marker is set and every column of content is cleared, the key of the item with them, in one statement; an item emptied already is left as it is.
    /// </summary>
    internal const string EmptyItemText =
        "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL, occurrences = NULL, dek_epoch = NULL, dek_wrapped = NULL "
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
        "SELECT dek_epoch FROM content.item_text WHERE emptied_at IS NULL UNION SELECT dek_epoch FROM content.reversal_map WHERE emptied_at IS NULL "
        + "UNION SELECT dek_epoch FROM content.batch_origin";

    /// <summary>The queries over content, as the statement each one runs, by the name of its constant.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = Declared(typeof(ContentQueries));

    /// <summary>The public and internal string constants the type declares, by name; a public constant is refused by the analyzer, so the queries are internal.</summary>
    internal static FrozenDictionary<string, string> Declared(Type type) =>
        type.GetRuntimeFields()
            .Where(field => field.DeclaringType == type && field.IsLiteral && !field.IsPrivate && field.FieldType == typeof(string))
            .ToFrozenDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
}
