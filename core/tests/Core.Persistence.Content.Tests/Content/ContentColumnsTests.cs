using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>The lists of columns say, for each column of the content schema, whether it is stored in clear or sealed, and nothing is both.</summary>
public sealed class ContentColumnsTests
{
    [Fact]
    public void PseudonymizedTextIsSealedWithTheKeyOfTheItem() =>
        Assert.Equal(
            (true, false, LabelPurpose.RawText),
            (ContentColumns.Protected.Contains(ContentColumns.PseudonymizedText), ContentColumns.Clear.Any(column => column.Name == "pseudonymized_text"),
             ContentColumns.PseudonymizedText.Purpose));

    [Fact]
    public void NoColumnIsBothInClearAndProtected()
    {
        var clear = ContentColumns.Clear.Select(column => (column.Table, column.Name));
        var sealedColumns = ContentColumns.Protected.Select(column => (column.Table, column.Name));

        Assert.Empty(clear.Intersect(sealedColumns));
    }

    [Fact]
    public void NoColumnIsListedTwice()
    {
        var all = ContentColumns.Clear.Select(column => (column.Table, column.Name))
            .Concat(ContentColumns.Protected.Select(column => (column.Table, column.Name)))
            .ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void RawSanitizedAndOccurrencesShareThePurposeAndTheMapHasItsOwn()
    {
        Assert.Equal(
            [LabelPurpose.RawText, LabelPurpose.RawText, LabelPurpose.RawText, LabelPurpose.ReversalMap],
            [ContentColumns.RawText.Purpose, ContentColumns.SanitizedText.Purpose, ContentColumns.Occurrences.Purpose, ContentColumns.Entries.Purpose]);
    }

    [Fact]
    public void EveryTableHasTheDomainAxisInClear()
    {
        var tables = ContentColumns.Clear.Select(column => column.Table).Concat(ContentColumns.Protected.Select(column => column.Table)).Distinct();

        Assert.All(tables, table => Assert.Contains(ContentColumns.Clear, column => column.Table == table && column.Kind == ClearKind.Domain));
    }

    [Fact]
    public void ProtectedColumnsAreExactlyTheOnesTheMigrationAndTheRuleNameByTable() =>
        Assert.Equal(
            [
                new ProtectedColumn("item_text", "raw_text", LabelPurpose.RawText),
                new ProtectedColumn("item_text", "sanitized_text", LabelPurpose.RawText),
                new ProtectedColumn("item_text", "occurrences", LabelPurpose.RawText),
                new ProtectedColumn("item_text", "sanitization_trail", LabelPurpose.RawText),
                new ProtectedColumn("item_text", "dek_wrapped", LabelPurpose.RawText),
                new ProtectedColumn("reversal_map", "entries", LabelPurpose.ReversalMap),
                new ProtectedColumn("reversal_map", "dek_wrapped", LabelPurpose.ReversalMap),
                new ProtectedColumn("batch_origin", "origin", LabelPurpose.RawText),
                new ProtectedColumn("batch_origin", "dek_wrapped", LabelPurpose.RawText),
                new ProtectedColumn("date_shift", "shift", LabelPurpose.DateShift),
                new ProtectedColumn("date_shift", "dek_wrapped", LabelPurpose.DateShift),
                new ProtectedColumn("item_text", "pseudonymized_text", LabelPurpose.RawText),
                new ProtectedColumn("reversal_request", "justification", LabelPurpose.ReversalMap),
                new ProtectedColumn("reversal_request", "dek_wrapped", LabelPurpose.ReversalMap),
            ],
            ContentColumns.Protected);

    [Fact]
    public void ClearColumnsAreExactlyTheOnesTheMigrationAndTheRuleNameByTable() =>
        Assert.Equal(
            [
                new ClearColumn("item_text", "id", ClearKind.Identifier),
                new ClearColumn("item_text", "domain_id", ClearKind.Domain),
                new ClearColumn("item_text", "recorded_at", ClearKind.Moment),
                new ClearColumn("item_text", "emptied_at", ClearKind.Moment),
                new ClearColumn("item_text", "dek_epoch", ClearKind.Epoch),
                new ClearColumn("item_text", "batch_id", ClearKind.Identifier),
                new ClearColumn("item_text", "written_in", ClearKind.Transaction),
                new ClearColumn("item_text", "accommodated_into", ClearKind.Motive),
                new ClearColumn("item_text", "losses", ClearKind.Motive),
                new ClearColumn("item_text", "removed_passages", ClearKind.Epoch),
                new ClearColumn("item_text", "pseudonymized_passages", ClearKind.Epoch),
                new ClearColumn("item_text", "preserved_passages", ClearKind.Epoch),
                new ClearColumn("item_text", "sanitization_version", ClearKind.Motive),
                new ClearColumn("item_text", "sanitization_counts", ClearKind.Counts),
                new ClearColumn("item_text", "sanitization_untreated", ClearKind.Counts),
                new ClearColumn("sanitization_version", "id", ClearKind.Identifier),
                new ClearColumn("sanitization_version", "domain_id", ClearKind.Domain),
                new ClearColumn("sanitization_version", "version", ClearKind.Motive),
                new ClearColumn("sanitization_version", "steps", ClearKind.Steps),
                new ClearColumn("reversal_map", "id", ClearKind.Identifier),
                new ClearColumn("reversal_map", "domain_id", ClearKind.Domain),
                new ClearColumn("reversal_map", "item_id", ClearKind.Identifier),
                new ClearColumn("reversal_map", "recorded_at", ClearKind.Moment),
                new ClearColumn("reversal_map", "emptied_at", ClearKind.Moment),
                new ClearColumn("reversal_map", "dek_epoch", ClearKind.Epoch),
                new ClearColumn("reversal_map", "batch_id", ClearKind.Identifier),
                new ClearColumn("reversal_map", "written_in", ClearKind.Transaction),
                new ClearColumn("item_emptied", "id", ClearKind.Identifier),
                new ClearColumn("item_emptied", "domain_id", ClearKind.Domain),
                new ClearColumn("item_emptied", "item_id", ClearKind.Identifier),
                new ClearColumn("ingestion_batch", "id", ClearKind.Identifier),
                new ClearColumn("ingestion_batch", "domain_id", ClearKind.Domain),
                new ClearColumn("ingestion_batch", "state", ClearKind.Status),
                new ClearColumn("ingestion_batch", "motive", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "condition", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "opened_by", ClearKind.Identifier),
                new ClearColumn("ingestion_batch", "opened_at", ClearKind.Moment),
                new ClearColumn("ingestion_batch", "closed_at", ClearKind.Moment),
                new ClearColumn("ingestion_batch", "submission", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "received_bytes", ClearKind.Identifier),
                new ClearColumn("ingestion_batch", "received_digest", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "warning", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "origin_state", ClearKind.Motive),
                new ClearColumn("batch_origin", "id", ClearKind.Identifier),
                new ClearColumn("batch_origin", "domain_id", ClearKind.Domain),
                new ClearColumn("batch_origin", "batch_id", ClearKind.Identifier),
                new ClearColumn("batch_origin", "recorded_at", ClearKind.Moment),
                new ClearColumn("batch_origin", "dek_epoch", ClearKind.Epoch),
                new ClearColumn("date_shift", "id", ClearKind.Identifier),
                new ClearColumn("date_shift", "domain_id", ClearKind.Domain),
                new ClearColumn("date_shift", "drawn_at", ClearKind.Moment),
                new ClearColumn("date_shift", "dek_epoch", ClearKind.Epoch),
                new ClearColumn("ingestion_batch", "review_sample", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "sampled_at", ClearKind.Moment),
                new ClearColumn("ingestion_batch", "reviewed_by", ClearKind.Identifier),
                new ClearColumn("ingestion_batch", "reviewed_at", ClearKind.Moment),
                new ClearColumn("ingestion_batch", "review_finding", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "declared_form", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "source_form", ClearKind.Motive),
                new ClearColumn("ingestion_batch", "reviewed_position", ClearKind.Epoch),
                new ClearColumn("ingestion_batch", "finding_position", ClearKind.Epoch),
                new ClearColumn("replication_occurrence", "id", ClearKind.Identifier),
                new ClearColumn("replication_occurrence", "domain_id", ClearKind.Domain),
                new ClearColumn("replication_occurrence", "batch_id", ClearKind.Identifier),
                new ClearColumn("replication_occurrence", "item_id", ClearKind.Identifier),
                new ClearColumn("replication_occurrence", "message_position", ClearKind.Epoch),
                new ClearColumn("replication_occurrence", "text_digest", ClearKind.Motive),
                new ClearColumn("replication_occurrence", "sender_digest", ClearKind.Motive),
                new ClearColumn("replication_occurrence", "text_length", ClearKind.Epoch),
                new ClearColumn("replication_occurrence", "said_at", ClearKind.Moment),
                new ClearColumn("replication_occurrence", "written_in", ClearKind.Transaction),
                new ClearColumn("replication_mark", "id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "domain_id", ClearKind.Domain),
                new ClearColumn("replication_mark", "batch_id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "item_id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "message_position", ClearKind.Epoch),
                new ClearColumn("replication_mark", "first_item_id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "first_message_position", ClearKind.Epoch),
                new ClearColumn("replication_mark", "written_in", ClearKind.Transaction),
                new ClearColumn("sanitization_rerun", "id", ClearKind.Identifier),
                new ClearColumn("sanitization_rerun", "domain_id", ClearKind.Domain),
                new ClearColumn("sanitization_rerun", "target_version", ClearKind.Motive),
                new ClearColumn("sanitization_rerun", "requested_by", ClearKind.Identifier),
                new ClearColumn("sanitization_rerun", "requested_at", ClearKind.Moment),
                new ClearColumn("marker_in_use", "id", ClearKind.Identifier),
                new ClearColumn("marker_in_use", "domain_id", ClearKind.Domain),
                new ClearColumn("marker_in_use", "marker", ClearKind.Motive),
                new ClearColumn("marker_in_use", "kind", ClearKind.Motive),
                new ClearColumn("item_marker", "id", ClearKind.Identifier),
                new ClearColumn("item_marker", "domain_id", ClearKind.Domain),
                new ClearColumn("item_marker", "item_id", ClearKind.Identifier),
                new ClearColumn("item_marker", "marker", ClearKind.Motive),
                new ClearColumn("item_marker", "author", ClearKind.Flag),
                new ClearColumn("sanitization_rerun_item", "id", ClearKind.Identifier),
                new ClearColumn("sanitization_rerun_item", "domain_id", ClearKind.Domain),
                new ClearColumn("sanitization_rerun_item", "rerun_id", ClearKind.Identifier),
                new ClearColumn("sanitization_rerun_item", "item_id", ClearKind.Identifier),
                new ClearColumn("sanitization_rerun_item", "state", ClearKind.Status),
                new ClearColumn("sanitization_rerun_item", "hold_until", ClearKind.Moment),
                new ClearColumn("sanitization_rerun_item", "tries", ClearKind.Epoch),
                new ClearColumn("sanitization_rerun_item", "concluded_at", ClearKind.Moment),
                new ClearColumn("sanitization_rerun_item", "result", ClearKind.Motive),
                new ClearColumn("sanitization_rerun_item", "refusal", ClearKind.Motive),
                new ClearColumn("reversal_request", "id", ClearKind.Identifier),
                new ClearColumn("reversal_request", "domain_id", ClearKind.Domain),
                new ClearColumn("reversal_request", "item_id", ClearKind.Identifier),
                new ClearColumn("reversal_request", "marker", ClearKind.Motive),
                new ClearColumn("reversal_request", "requested_by", ClearKind.Identifier),
                new ClearColumn("reversal_request", "requested_at", ClearKind.Moment),
                new ClearColumn("reversal_request", "term_days", ClearKind.Epoch),
                new ClearColumn("reversal_request", "dek_epoch", ClearKind.Epoch),
                new ClearColumn("reversal_authorization", "id", ClearKind.Identifier),
                new ClearColumn("reversal_authorization", "domain_id", ClearKind.Domain),
                new ClearColumn("reversal_authorization", "request_id", ClearKind.Identifier),
                new ClearColumn("reversal_authorization", "authorized_by", ClearKind.Identifier),
                new ClearColumn("reversal_authorization", "authorized_at", ClearKind.Moment),
                new ClearColumn("reversal_authorization", "valid_until", ClearKind.Moment),
                new ClearColumn("reversal_revelation", "id", ClearKind.Identifier),
                new ClearColumn("reversal_revelation", "domain_id", ClearKind.Domain),
                new ClearColumn("reversal_revelation", "authorization_id", ClearKind.Identifier),
                new ClearColumn("reversal_revelation", "revealed_at", ClearKind.Moment),
            ],
            ContentColumns.Clear);

    [Fact]
    public void TablesAreNamedWithoutTheSchema() =>
        Assert.Equal(["item_text", "reversal_map"], [ContentColumns.ItemText, ContentColumns.ReversalMap]);

    [Fact]
    public void MarkOfAColumnSaysTheLabelOfItsPurposeAndNothingOfTheDomain() =>
        Assert.Equal(
            [
                "protected:texto-bruto", "protected:texto-bruto", "protected:texto-bruto", "protected:texto-bruto", "protected:texto-bruto", "protected:mapa-reversao",
                "protected:mapa-reversao",
                "protected:texto-bruto", "protected:texto-bruto",
                "protected:deslocamento-datas", "protected:deslocamento-datas", "protected:texto-bruto", "protected:mapa-reversao", "protected:mapa-reversao",
            ],
            ContentColumns.Protected.Select(column => column.Mark));
}
