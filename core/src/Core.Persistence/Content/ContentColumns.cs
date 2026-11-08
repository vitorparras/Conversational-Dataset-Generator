using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>
/// Every column of the content schema, in the list of those stored in clear and the list of those stored sealed. A column in neither list is a
/// defect, and the check of the schema at startup refuses it; a column enters the second list the moment it holds text that is not pseudonymized.
/// </summary>
public static class ContentColumns
{
    private const string DomainColumn = "domain_id";

    /// <summary>The table of the texts of an item.</summary>
    public static string ItemText { get; } = "item_text";

    /// <summary>The table of the maps that reverse a pseudonym.</summary>
    public static string ReversalMap { get; } = "reversal_map";

    /// <summary>The table of the batches of ingestion, the unit in which content comes in; the content of a batch is visible only once the batch is accepted.</summary>
    public static string IngestionBatch { get; } = "ingestion_batch";

    /// <summary>The table that holds the id of each item emptied, so that the refusals of the database do not read the texts.</summary>
    public static string ItemEmptied { get; } = "item_emptied";

    /// <summary>The table of the digests of every message, by item and position: what the replicated content is compared by, never the text or the sender.</summary>
    public static string ReplicationOccurrence { get; } = "replication_occurrence";

    /// <summary>The table of the marks of the messages whose content is replicated, each pointing to the first occurrence.</summary>
    public static string ReplicationMark { get; } = "replication_mark";

    /// <summary>The text as it arrived.</summary>
    public static ProtectedColumn RawText { get; } = new(ItemText, "raw_text", LabelPurpose.RawText);

    /// <summary>The text after sanitization, which has the key and the authorization of the raw text.</summary>
    public static ProtectedColumn SanitizedText { get; } = new(ItemText, "sanitized_text", LabelPurpose.RawText);

    /// <summary>What each pseudonym replaced.</summary>
    public static ProtectedColumn Entries { get; } = new(ReversalMap, "entries", LabelPurpose.ReversalMap);

    /// <summary>The key of an item, random and wrapped under the key of the domain and the epoch; it seals the raw and the sanitized text, and is destroyed with them.</summary>
    public static ProtectedColumn ItemKey { get; } = new(ItemText, "dek_wrapped", LabelPurpose.RawText);

    /// <summary>The key of a map, as the key of an item is, for the entries.</summary>
    public static ProtectedColumn MapKey { get; } = new(ReversalMap, "dek_wrapped", LabelPurpose.ReversalMap);

    /// <summary>The columns stored sealed.</summary>
    public static IReadOnlyList<ProtectedColumn> Protected { get; } = [RawText, SanitizedText, ItemKey, Entries, MapKey];

    /// <summary>The columns stored in clear: identifiers, the domain, dates and the pseudonymized text.</summary>
    public static IReadOnlyList<ClearColumn> Clear { get; } =
    [
        new(ItemText, "id", ClearKind.Identifier),
        new(ItemText, DomainColumn, ClearKind.Domain),
        new(ItemText, "recorded_at", ClearKind.Moment),
        new(ItemText, "emptied_at", ClearKind.Moment),
        new(ItemText, "pseudonymized_text", ClearKind.PseudonymizedText),
        new(ItemText, "dek_epoch", ClearKind.Epoch),
        new(ItemText, "batch_id", ClearKind.Identifier),
        new(ItemText, "written_in", ClearKind.Transaction),
        new(ReversalMap, "id", ClearKind.Identifier),
        new(ReversalMap, DomainColumn, ClearKind.Domain),
        new(ReversalMap, "item_id", ClearKind.Identifier),
        new(ReversalMap, "recorded_at", ClearKind.Moment),
        new(ReversalMap, "emptied_at", ClearKind.Moment),
        new(ReversalMap, "dek_epoch", ClearKind.Epoch),
        new(ReversalMap, "batch_id", ClearKind.Identifier),
        new(ReversalMap, "written_in", ClearKind.Transaction),
        new(ItemEmptied, "id", ClearKind.Identifier),
        new(ItemEmptied, DomainColumn, ClearKind.Domain),
        new(ItemEmptied, "item_id", ClearKind.Identifier),
        new(IngestionBatch, "id", ClearKind.Identifier),
        new(IngestionBatch, DomainColumn, ClearKind.Domain),
        new(IngestionBatch, "state", ClearKind.Status),
        new(IngestionBatch, "motive", ClearKind.Motive),
        new(IngestionBatch, "opened_by", ClearKind.Identifier),
        new(IngestionBatch, "opened_at", ClearKind.Moment),
        new(IngestionBatch, "closed_at", ClearKind.Moment),
        new(ReplicationOccurrence, "id", ClearKind.Identifier),
        new(ReplicationOccurrence, DomainColumn, ClearKind.Domain),
        new(ReplicationOccurrence, "batch_id", ClearKind.Identifier),
        new(ReplicationOccurrence, "item_id", ClearKind.Identifier),
        new(ReplicationOccurrence, "message_position", ClearKind.Position),
        new(ReplicationOccurrence, "text_digest", ClearKind.Digest),
        new(ReplicationOccurrence, "sender_digest", ClearKind.Digest),
        new(ReplicationOccurrence, "text_length", ClearKind.Length),
        new(ReplicationOccurrence, "said_at", ClearKind.Moment),
        new(ReplicationOccurrence, "written_in", ClearKind.Transaction),
        new(ReplicationMark, "id", ClearKind.Identifier),
        new(ReplicationMark, DomainColumn, ClearKind.Domain),
        new(ReplicationMark, "batch_id", ClearKind.Identifier),
        new(ReplicationMark, "item_id", ClearKind.Identifier),
        new(ReplicationMark, "message_position", ClearKind.Position),
        new(ReplicationMark, "first_item_id", ClearKind.Identifier),
        new(ReplicationMark, "first_message_position", ClearKind.Position),
        new(ReplicationMark, "written_in", ClearKind.Transaction),
    ];
}
