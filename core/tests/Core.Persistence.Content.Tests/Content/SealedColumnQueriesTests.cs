using Core.Persistence.Content;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The database never looks into a sealed column (issue 79, CA-3): when a query over content is assembled, no filter, order, group, join, comparison or function falls on the raw text,
/// the sanitized text, the entries of a map, the origin or the wrapped key; those columns only pass through, and any filter over the raw text happens after the application opens it.
/// </summary>
public sealed class SealedColumnQueriesTests
{
    private static readonly string[] Sealed = [.. ContentColumns.Protected.Select(column => column.Name).Distinct(StringComparer.Ordinal)];

    [Fact]
    public void NoQueryOverContentFiltersOrdersOrComparesBySealedColumn()
    {
        var uses = ContentQueries.All.Where(query => SealedColumnUse.Beyond(query.Value, Sealed).Count > 0).Select(query => query.Key);

        Assert.Empty(uses);
    }

    [Fact]
    public void TheSealedColumnsAreTheOnesTheContentMarks() =>
        Assert.Equal(["raw_text", "sanitized_text", "dek_wrapped", "entries", "origin"], Sealed);

    [Theory]
    [InlineData("SELECT id FROM content.item_text WHERE raw_text = @x")]
    [InlineData("SELECT id FROM content.item_text WHERE raw_text LIKE '%x%'")]
    [InlineData("SELECT id FROM content.item_text ORDER BY raw_text")]
    [InlineData("SELECT count(*) FROM content.item_text GROUP BY sanitized_text")]
    [InlineData("SELECT id FROM content.reversal_map WHERE length(entries) > 3")]
    [InlineData("SELECT a.id FROM content.batch_origin a JOIN content.batch_origin b ON a.origin = b.origin")]
    [InlineData("SELECT DISTINCT raw_text FROM content.item_text")]
    [InlineData("SELECT id FROM content.item_text WHERE dek_wrapped IS NULL")]
    [InlineData("UPDATE content.item_text SET dek_epoch = @e WHERE raw_text = @x")]
    [InlineData("DELETE FROM content.item_text WHERE raw_text = @x")]
    [InlineData("SELECT id FROM content.batch_origin WHERE origin = @origin")]
    [InlineData("SELECT id FROM content.reversal_map WHERE entries IS NOT NULL AND id = @entries")]
    [InlineData("SELECT DISTINCT origin FROM content.batch_origin WHERE id = @origin")]
    public void FilterOrderGroupJoinComparisonOrFunctionOverASealedColumnIsFound(string statement) =>
        Assert.NotEmpty(SealedColumnUse.Beyond(statement, Sealed));

    [Theory]
    [InlineData("SELECT raw_text, dek_epoch, dek_wrapped FROM content.item_text WHERE id = @id")]
    [InlineData("INSERT INTO content.item_text (domain_id, raw_text, dek_wrapped) VALUES (@d, @r, @w) RETURNING id")]
    [InlineData("UPDATE content.item_text SET raw_text = NULL, dek_wrapped = NULL WHERE id = @id")]
    [InlineData("SELECT pg_catalog.to_jsonb(t)::text FROM content.batch_origin t ORDER BY id")]
    [InlineData("INSERT INTO content.batch_origin (batch_id, domain_id, origin, dek_wrapped) VALUES (@batch, @domain, @origin, @wrapped)")]
    [InlineData("INSERT INTO content.reversal_map (domain_id, entries, dek_wrapped) VALUES (@domain, @entries, @wrapped) RETURNING id")]
    [InlineData("UPDATE content.batch_origin SET origin = @origin, dek_wrapped = @wrapped WHERE id = @id")]
    public void ColumnThatOnlyPassesThroughIsNotFound(string statement) =>
        Assert.Empty(SealedColumnUse.Beyond(statement, Sealed));
}
