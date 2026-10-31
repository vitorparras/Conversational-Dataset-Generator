using Core.Persistence.Content;
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
                new ProtectedColumn("reversal_map", "entries", LabelPurpose.ReversalMap),
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
                new ClearColumn("reversal_map", "id", ClearKind.Identifier),
                new ClearColumn("reversal_map", "domain_id", ClearKind.Domain),
                new ClearColumn("reversal_map", "item_id", ClearKind.Identifier),
                new ClearColumn("reversal_map", "recorded_at", ClearKind.Moment),
                new ClearColumn("reversal_map", "emptied_at", ClearKind.Moment),
            ],
            ContentColumns.Clear);

    [Fact]
    public void TablesAreNamedWithoutTheSchema() =>
        Assert.Equal(["item_text", "reversal_map"], [ContentColumns.ItemText, ContentColumns.ReversalMap]);

    [Fact]
    public void MarkOfAColumnSaysTheLabelOfItsPurposeAndNothingOfTheDomain() =>
        Assert.Equal(
            ["protected:texto-bruto", "protected:texto-bruto", "protected:mapa-reversao"],
            ContentColumns.Protected.Select(column => column.Mark));
}
