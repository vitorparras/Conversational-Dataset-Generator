using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>The lists of columns say, for each column of the content schema, whether it is stored in clear or sealed, and nothing is both.</summary>
public sealed class ContentColumnsTests
{
    [Fact]
    public void PseudonymizedTextIsTheOnlyTextStoredInClear()
    {
        var texts = ContentColumns.Clear.Where(column => column.Kind == ClearKind.PseudonymizedText);

        Assert.Equal([(ContentColumns.ItemText, "pseudonymized_text")], texts.Select(column => (column.Table, column.Name)));
    }

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
    public void RawAndSanitizedTextShareThePurposeAndTheMapHasItsOwn()
    {
        Assert.Equal(
            [LabelPurpose.RawText, LabelPurpose.RawText, LabelPurpose.ReversalMap],
            [ContentColumns.RawText.Purpose, ContentColumns.SanitizedText.Purpose, ContentColumns.Entries.Purpose]);
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
                new ProtectedColumn("item_text", "dek_wrapped", LabelPurpose.RawText),
                new ProtectedColumn("reversal_map", "entries", LabelPurpose.ReversalMap),
                new ProtectedColumn("reversal_map", "dek_wrapped", LabelPurpose.ReversalMap),
                new ProtectedColumn("batch_origin", "origin", LabelPurpose.RawText),
                new ProtectedColumn("batch_origin", "dek_wrapped", LabelPurpose.RawText),
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
                new ClearColumn("item_text", "pseudonymized_text", ClearKind.PseudonymizedText),
                new ClearColumn("item_text", "dek_epoch", ClearKind.Epoch),
                new ClearColumn("item_text", "batch_id", ClearKind.Identifier),
                new ClearColumn("item_text", "written_in", ClearKind.Transaction),
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
                new ClearColumn("replication_occurrence", "id", ClearKind.Identifier),
                new ClearColumn("replication_occurrence", "domain_id", ClearKind.Domain),
                new ClearColumn("replication_occurrence", "batch_id", ClearKind.Identifier),
                new ClearColumn("replication_occurrence", "item_id", ClearKind.Identifier),
                new ClearColumn("replication_occurrence", "message_position", ClearKind.Position),
                new ClearColumn("replication_occurrence", "text_digest", ClearKind.Digest),
                new ClearColumn("replication_occurrence", "sender_digest", ClearKind.Digest),
                new ClearColumn("replication_occurrence", "text_length", ClearKind.Length),
                new ClearColumn("replication_occurrence", "said_at", ClearKind.Moment),
                new ClearColumn("replication_occurrence", "written_in", ClearKind.Transaction),
                new ClearColumn("replication_mark", "id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "domain_id", ClearKind.Domain),
                new ClearColumn("replication_mark", "batch_id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "item_id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "message_position", ClearKind.Position),
                new ClearColumn("replication_mark", "first_item_id", ClearKind.Identifier),
                new ClearColumn("replication_mark", "first_message_position", ClearKind.Position),
                new ClearColumn("replication_mark", "written_in", ClearKind.Transaction),
            ],
            ContentColumns.Clear);

    [Fact]
    public void TablesAreNamedWithoutTheSchema() =>
        Assert.Equal(["item_text", "reversal_map"], [ContentColumns.ItemText, ContentColumns.ReversalMap]);

    [Fact]
    public void MarkOfAColumnSaysTheLabelOfItsPurposeAndNothingOfTheDomain() =>
        Assert.Equal(
            [
                "protected:texto-bruto", "protected:texto-bruto", "protected:texto-bruto", "protected:mapa-reversao", "protected:mapa-reversao",
                "protected:texto-bruto", "protected:texto-bruto",
            ],
            ContentColumns.Protected.Select(column => column.Mark));
}
