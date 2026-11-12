using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// What the review of a batch refuses before it touches the database, each by the name of what was refused, and what a finding removes: the material of that batch and no other.
/// </summary>
public sealed class BatchReviewGuardsTests
{
    private const string Offline = "Host=127.0.0.1;Port=1;Username=u;Database=d";

    [Fact]
    public async Task ASamplingWithoutARequestIsRefused()
    {
        await using var dataSource = NpgsqlDataSource.Create(Offline);

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => new BatchReviews(dataSource, 3).DrawAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("request", refused.ParamName);
    }

    [Theory]
    [InlineData(0, 1, 1, "request.DomainId")]
    [InlineData(1, 0, 1, "request.BatchId")]
    [InlineData(1, 1, 0, "request.ReviewerId")]
    public async Task ASamplingNamedByAnIdentifierThatIsNotPositiveIsRefusedByThatIdentifier(int domain, long batch, long reviewer, string name)
    {
        await using var dataSource = NpgsqlDataSource.Create(Offline);

        var reviews = new BatchReviews(dataSource, 3);
        var request = new BatchSampling(domain, batch, reviewer);

        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reviews.DrawAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(name, refused.ParamName);
    }

    [Theory]
    [InlineData(0, 1, 1, "request.DomainId")]
    [InlineData(1, 0, 1, "request.BatchId")]
    [InlineData(1, 1, 0, "request.ReviewerId")]
    public async Task AReviewNamedByAnIdentifierThatIsNotPositiveIsRefusedByThatIdentifier(int domain, long batch, long reviewer, string name)
    {
        await using var dataSource = NpgsqlDataSource.Create(Offline);

        var reviews = new BatchReviews(dataSource, 3);
        var request = new BatchReviewing(domain, batch, reviewer);

        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reviews.ReviewAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(name, refused.ParamName);
    }

    [Theory]
    [InlineData(0, 1, "domainId")]
    [InlineData(1, 0, "batchId")]
    public async Task AReadingOfAnIdentifierThatIsNotPositiveIsRefusedByThatIdentifier(int domain, long batch, string name)
    {
        await using var dataSource = NpgsqlDataSource.Create(Offline);

        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new BatchReviews(dataSource, 3).ReadAsync(domain, batch, TestContext.Current.CancellationToken));

        Assert.Equal(name, refused.ParamName);
    }

    [DatabaseFact]
    public async Task AFindingRemovesTheItemsAndMapsOfThatBatchAndOfNoOther()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await IngestionWorld.CreateAsync();
        var found = await IngestionWorld.OpenAsync(world);
        var kept = await IngestionWorld.OpenAsync(world);
        var foundItem = await IngestionWorld.AddItemAsync(world, found);
        var keptItem = await IngestionWorld.AddItemAsync(world, kept);
        _ = await IngestionWorld.AddMapAsync(world, found, foundItem);
        _ = await IngestionWorld.AddMapAsync(world, kept, keptItem);
        var reviews = new BatchReviews(world.DataSource, 3);
        _ = await reviews.DrawAsync(new BatchSampling(IngestionWorld.Domain, found, IngestionWorld.Person), cancellationToken);

        _ = await reviews.ReviewAsync(new BatchReviewing(IngestionWorld.Domain, found, IngestionWorld.Person, "contact_not_treated"), cancellationToken);

        Assert.Equal(("0,0", "1,1"), (await RowsAsync(world, found), await RowsAsync(world, kept)));
    }

    private static async Task<string> RowsAsync(BatchesWorld world, long batch)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transaction = await ContentTransaction.BeginAsync(world.DataSource, IngestionWorld.Domain, cancellationToken);
        await using (var scope = transaction.CreateCommand("SELECT set_config('app.content_scope', 'system', true)"))
        {
            _ = await scope.ExecuteScalarAsync(cancellationToken);
        }

        await using var count = transaction.CreateCommand(
            "SELECT (SELECT count(*) FROM content.item_text WHERE batch_id = @batch) || ',' || (SELECT count(*) FROM content.reversal_map WHERE batch_id = @batch)");
        _ = count.Parameters.AddWithValue("batch", batch);
        return (string)(await count.ExecuteScalarAsync(cancellationToken))!;
    }
}
