using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>
/// Every column of the content schema, in the list of those stored in clear and the list of those stored sealed. A column in neither list is a
/// defect, and the check of the schema at startup refuses it; a column enters the second list the moment it holds text that is not pseudonymized.
/// </summary>
public static class ContentColumns
{
    private const string IdColumn = "id";
    private const string DomainColumn = "domain_id";
    private const string BatchColumn = "batch_id";
    private const string WrittenInColumn = "written_in";
    private const string ItemColumn = "item_id";
    private const string EpochColumn = "dek_epoch";
    private const string RerunTable = "sanitization_rerun";
    private const string RerunItemTable = "sanitization_rerun_item";

    /// <summary>The table of the texts of an item.</summary>
    public static string ItemText { get; } = "item_text";

    /// <summary>The table of the maps that reverse a pseudonym.</summary>
    public static string ReversalMap { get; } = "reversal_map";

    /// <summary>The table of the batches of ingestion, the unit in which content comes in; the content of a batch is visible only once the batch is accepted.</summary>
    public static string IngestionBatch { get; } = "ingestion_batch";

    /// <summary>The table of the origins sealed, one for each batch whose submission declared where the material came from.</summary>
    public static string BatchOrigin { get; } = "batch_origin";

    /// <summary>The table that holds the id of each item emptied, so that the refusals of the database do not read the texts.</summary>
    public static string ItemEmptied { get; } = "item_emptied";

    /// <summary>The table of the digests of every message, by item and position: what the replicated content is compared by, never the text or the sender.</summary>
    public static string ReplicationOccurrence { get; } = "replication_occurrence";

    /// <summary>The table of the marks of the messages whose content is replicated, each pointing to the first occurrence.</summary>
    public static string ReplicationMark { get; } = "replication_mark";

    /// <summary>The table of the versions of the list of sanitization steps, written once per domain: names of steps and numbers, never material.</summary>
    public static string SanitizationVersion { get; } = "sanitization_version";

    /// <summary>The text as it arrived.</summary>
    public static ProtectedColumn RawText { get; } = new(ItemText, "raw_text", LabelPurpose.RawText);

    /// <summary>The text after sanitization, which has the key and the authorization of the raw text.</summary>
    public static ProtectedColumn SanitizedText { get; } = new(ItemText, "sanitized_text", LabelPurpose.RawText);

    /// <summary>The occurrences found in the raw text and the destination of each; with the offsets they rebuild what was found, so they are sealed like the raw text.</summary>
    public static ProtectedColumn Occurrences { get; } = new(ItemText, "occurrences", LabelPurpose.RawText);

    /// <summary>The trail of the sanitization of an item: with the raw text its positions rebuild what was removed, so it is sealed like the raw text.</summary>
    public static ProtectedColumn SanitizationTrail { get; } = new(ItemText, "sanitization_trail", LabelPurpose.RawText);

    /// <summary>What each pseudonym replaced.</summary>
    public static ProtectedColumn Entries { get; } = new(ReversalMap, "entries", LabelPurpose.ReversalMap);

    /// <summary>The key of an item, random and wrapped under the key of the domain and the epoch; it seals the raw and the sanitized text, and is destroyed with them.</summary>
    public static ProtectedColumn ItemKey { get; } = new(ItemText, "dek_wrapped", LabelPurpose.RawText);

    /// <summary>The key of a map, as the key of an item is, for the entries.</summary>
    public static ProtectedColumn MapKey { get; } = new(ReversalMap, "dek_wrapped", LabelPurpose.ReversalMap);

    /// <summary>Where the material of a batch came from, as its submission declared it, sealed as the raw text is (decision 0025).</summary>
    public static ProtectedColumn Origin { get; } = new(BatchOrigin, "origin", LabelPurpose.RawText);

    /// <summary>The key of an origin, as the key of an item is, for the origin.</summary>
    public static ProtectedColumn OriginKey { get; } = new(BatchOrigin, "dek_wrapped", LabelPurpose.RawText);

    /// <summary>The table of the shift of the dates of each domain, one row for each, drawn once (decision 0020).</summary>
    public static string DateShiftTable { get; } = "date_shift";

    /// <summary>The shift of the dates of a domain, in days, which nobody is told: sealed with a purpose of its own.</summary>
    public static ProtectedColumn DateShift { get; } = new(DateShiftTable, "shift", LabelPurpose.DateShift);

    /// <summary>The key of the shift, in the column every wrapped key has, as the key of an item is, for the shift.</summary>
    public static ProtectedColumn DateShiftKey { get; } = new(DateShiftTable, ItemKey.Name, LabelPurpose.DateShift);

    /// <summary>The columns stored sealed.</summary>
    public static IReadOnlyList<ProtectedColumn> Protected { get; } =
        [RawText, SanitizedText, Occurrences, SanitizationTrail, ItemKey, Entries, MapKey, Origin, OriginKey, DateShift, DateShiftKey];

    /// <summary>The columns stored in clear: identifiers, the domain, dates and the pseudonymized text.</summary>
    public static IReadOnlyList<ClearColumn> Clear { get; } =
    [
        new(ItemText, IdColumn, ClearKind.Identifier),
        new(ItemText, DomainColumn, ClearKind.Domain),
        new(ItemText, "recorded_at", ClearKind.Moment),
        new(ItemText, "emptied_at", ClearKind.Moment),
        new(ItemText, "pseudonymized_text", ClearKind.PseudonymizedText),
        new(ItemText, EpochColumn, ClearKind.Epoch),
        new(ItemText, BatchColumn, ClearKind.Identifier),
        new(ItemText, WrittenInColumn, ClearKind.Transaction),
        new(ItemText, "accommodated_into", ClearKind.Motive),
        new(ItemText, "losses", ClearKind.Motive),
        new(ItemText, "removed_passages", ClearKind.Epoch),
        new(ItemText, "pseudonymized_passages", ClearKind.Epoch),
        new(ItemText, "preserved_passages", ClearKind.Epoch),
        new(ItemText, "sanitization_version", ClearKind.Motive),
        new(ItemText, "sanitization_counts", ClearKind.Counts),
        new(ItemText, "sanitization_untreated", ClearKind.Counts),
        new(SanitizationVersion, IdColumn, ClearKind.Identifier),
        new(SanitizationVersion, DomainColumn, ClearKind.Domain),
        new(SanitizationVersion, "version", ClearKind.Motive),
        new(SanitizationVersion, "steps", ClearKind.Steps),
        new(ReversalMap, IdColumn, ClearKind.Identifier),
        new(ReversalMap, DomainColumn, ClearKind.Domain),
        new(ReversalMap, ItemColumn, ClearKind.Identifier),
        new(ReversalMap, "recorded_at", ClearKind.Moment),
        new(ReversalMap, "emptied_at", ClearKind.Moment),
        new(ReversalMap, EpochColumn, ClearKind.Epoch),
        new(ReversalMap, BatchColumn, ClearKind.Identifier),
        new(ReversalMap, WrittenInColumn, ClearKind.Transaction),
        new(ItemEmptied, IdColumn, ClearKind.Identifier),
        new(ItemEmptied, DomainColumn, ClearKind.Domain),
        new(ItemEmptied, ItemColumn, ClearKind.Identifier),
        new(IngestionBatch, IdColumn, ClearKind.Identifier),
        new(IngestionBatch, DomainColumn, ClearKind.Domain),
        new(IngestionBatch, "state", ClearKind.Status),
        new(IngestionBatch, "motive", ClearKind.Motive),
        new(IngestionBatch, "condition", ClearKind.Motive),
        new(IngestionBatch, "opened_by", ClearKind.Identifier),
        new(IngestionBatch, "opened_at", ClearKind.Moment),
        new(IngestionBatch, "closed_at", ClearKind.Moment),
        new(IngestionBatch, "submission", ClearKind.Motive),
        new(IngestionBatch, "received_bytes", ClearKind.Identifier),
        new(IngestionBatch, "received_digest", ClearKind.Motive),
        new(IngestionBatch, "warning", ClearKind.Motive),
        new(IngestionBatch, "origin_state", ClearKind.Motive),
        new(BatchOrigin, IdColumn, ClearKind.Identifier),
        new(BatchOrigin, DomainColumn, ClearKind.Domain),
        new(BatchOrigin, BatchColumn, ClearKind.Identifier),
        new(BatchOrigin, "recorded_at", ClearKind.Moment),
        new(BatchOrigin, EpochColumn, ClearKind.Epoch),
        new(DateShiftTable, IdColumn, ClearKind.Identifier),
        new(DateShiftTable, DomainColumn, ClearKind.Domain),
        new(DateShiftTable, "drawn_at", ClearKind.Moment),
        new(DateShiftTable, EpochColumn, ClearKind.Epoch),
        new(IngestionBatch, "review_sample", ClearKind.Motive),
        new(IngestionBatch, "sampled_at", ClearKind.Moment),
        new(IngestionBatch, "reviewed_by", ClearKind.Identifier),
        new(IngestionBatch, "reviewed_at", ClearKind.Moment),
        new(IngestionBatch, "review_finding", ClearKind.Motive),
        new(IngestionBatch, "declared_form", ClearKind.Motive),
        new(IngestionBatch, "source_form", ClearKind.Motive),
        new(IngestionBatch, "reviewed_position", ClearKind.Epoch),
        new(IngestionBatch, "finding_position", ClearKind.Epoch),
        new(ReplicationOccurrence, IdColumn, ClearKind.Identifier),
        new(ReplicationOccurrence, DomainColumn, ClearKind.Domain),
        new(ReplicationOccurrence, BatchColumn, ClearKind.Identifier),
        new(ReplicationOccurrence, ItemColumn, ClearKind.Identifier),
        new(ReplicationOccurrence, "message_position", ClearKind.Epoch),
        new(ReplicationOccurrence, "text_digest", ClearKind.Motive),
        new(ReplicationOccurrence, "sender_digest", ClearKind.Motive),
        new(ReplicationOccurrence, "text_length", ClearKind.Epoch),
        new(ReplicationOccurrence, "said_at", ClearKind.Moment),
        new(ReplicationOccurrence, WrittenInColumn, ClearKind.Transaction),
        new(ReplicationMark, IdColumn, ClearKind.Identifier),
        new(ReplicationMark, DomainColumn, ClearKind.Domain),
        new(ReplicationMark, BatchColumn, ClearKind.Identifier),
        new(ReplicationMark, ItemColumn, ClearKind.Identifier),
        new(ReplicationMark, "message_position", ClearKind.Epoch),
        new(ReplicationMark, "first_item_id", ClearKind.Identifier),
        new(ReplicationMark, "first_message_position", ClearKind.Epoch),
        new(ReplicationMark, WrittenInColumn, ClearKind.Transaction),
        new(RerunTable, IdColumn, ClearKind.Identifier),
        new(RerunTable, DomainColumn, ClearKind.Domain),
        new(RerunTable, "target_version", ClearKind.Motive),
        new(RerunTable, "requested_by", ClearKind.Identifier),
        new(RerunTable, "requested_at", ClearKind.Moment),
        new(RerunItemTable, IdColumn, ClearKind.Identifier),
        new(RerunItemTable, DomainColumn, ClearKind.Domain),
        new(RerunItemTable, "rerun_id", ClearKind.Identifier),
        new(RerunItemTable, ItemColumn, ClearKind.Identifier),
        new(RerunItemTable, "state", ClearKind.Status),
        new(RerunItemTable, "hold_until", ClearKind.Moment),
        new(RerunItemTable, "tries", ClearKind.Epoch),
        new(RerunItemTable, "concluded_at", ClearKind.Moment),
        new(RerunItemTable, "result", ClearKind.Motive),
        new(RerunItemTable, "refusal", ClearKind.Motive),
    ];
}
