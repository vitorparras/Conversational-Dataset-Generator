using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// How far the review of the sample went (issue 822, migration 0058): the last place marked grows by one,
/// only in an open batch whose sample was drawn and not yet reviewed, and the place of a finding is the one right after
/// the last marked, told only by a review that found something, whoever writes.
/// </summary>
public sealed class BatchReviewProgressTests
{
    private const string Mark = "UPDATE content.ingestion_batch SET reviewed_position = @position WHERE id = @id";
    private const string Position = "SELECT reviewed_position FROM content.ingestion_batch WHERE id = @id";
    private const string FindingPlace = "SELECT finding_position FROM content.ingestion_batch WHERE id = @id";

    [DatabaseFact]
    public async Task ReviewProgressGrowsOnlyByOne()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);

        await MarkAsync(world, batch, 1);
        var skipped = await Assert.ThrowsAsync<PostgresException>(() => MarkAsync(world, batch, 3));
        var repeated = await Assert.ThrowsAsync<PostgresException>(() => MarkAsync(world, batch, 1));
        await MarkAsync(world, batch, 2);

        Assert.Equal(("CE006", "CE006", 2), (skipped.SqlState, repeated.SqlState, await PositionAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task ReviewProgressNeverGoesBackOrBeyondTheSample()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);
        await MarkAsync(world, batch, 1);
        await MarkAsync(world, batch, 2);
        await MarkAsync(world, batch, 3);

        var back = await Assert.ThrowsAsync<PostgresException>(() => MarkAsync(world, batch, 1));
        var beyond = await Assert.ThrowsAsync<PostgresException>(() => MarkAsync(world, batch, 4));

        Assert.Equal(("CE006", "CE006", 3), (back.SqlState, beyond.SqlState, await PositionAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task ReviewProgressCannotGrowBeforeTheDraw()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => MarkAsync(world, batch, 1));

        Assert.Equal(("CE006", 0), (refused.SqlState, await PositionAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task ReviewProgressCannotGrowAfterTheReview()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);
        var reviewed = await Reviews(world).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => MarkAsync(world, batch, 1));

        Assert.Equal((BatchReviewOutcome.Reviewed, "CE006", 0), (reviewed.Outcome, refused.SqlState, await PositionAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task FindingPositionIsTheItemAfterTheLastReviewed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);
        await MarkAsync(world, batch, 1);
        await MarkAsync(world, batch, 2);

        var found = await Reviews(world).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person, "contact_not_treated"), TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.Rejected, 3), (found.Outcome, await FindingPlaceAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task FindingPositionNeedsARejectionForFinding()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var told = await DrawnAsync(world);
        var clean = await DrawnAsync(world);
        const string Tell = "UPDATE content.ingestion_batch SET finding_position = 1 WHERE id = @id";

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.AsSuperuserAsync(Tell, ("id", told)));
        var reviewed = await Reviews(world).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, clean, IngestionWorld.Person), TestContext.Current.CancellationToken);

        Assert.Equal(("CE006", BatchReviewOutcome.Reviewed, null), (refused.SqlState, reviewed.Outcome, await FindingPlaceAsync(world, clean)));
    }

    private static BatchReviews Reviews(BatchesWorld world) => new(world.DataSource, 3);

    private static async Task<int> PositionAsync(BatchesWorld world, long batch) => (int)(await world.AsSuperuserAsync(Position, ("id", batch)))!;

    private static async Task<int?> FindingPlaceAsync(BatchesWorld world, long batch) => (int?)await world.AsSuperuserAsync(FindingPlace, ("id", batch));

    private static async Task MarkAsync(BatchesWorld world, long batch, int position) => _ = await world.AsSuperuserAsync(Mark, ("position", position), ("id", batch));

    private static async Task<long> DrawnAsync(BatchesWorld world)
    {
        var batch = await IngestionWorld.OpenAsync(world);
        for (var item = 0; item < 3; item++)
        {
            _ = await IngestionWorld.AddItemAsync(world, batch);
        }

        var drawn = await Reviews(world).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        return batch;
    }
}
