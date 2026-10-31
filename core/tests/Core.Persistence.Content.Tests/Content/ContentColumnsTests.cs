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
}
