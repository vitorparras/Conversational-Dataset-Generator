using Core.Persistence.Content.Writing;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The review is the database's: no statement accepts a batch whose sample was not reviewed and found clean, the owner included,
/// and the sample, once drawn, neither changes nor lets another item in (issue 83, CA-5).
/// </summary>
[Trait("Category", "Database")]
public sealed class BatchReviewGateTests
{
    private const string WithoutEvent = "CE005";
    private const string OutsideTheRules = "CE006";
    private const string AcceptedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_accepted')";
    private const string Accept = "UPDATE content.ingestion_batch SET state = 'accepted' WHERE id = @id";
    private const string Rewrite = "UPDATE content.ingestion_batch SET review_sample = '1,2,3' WHERE id = @id";
    private const string Reviewer = "UPDATE content.ingestion_batch SET reviewed_by = 3 WHERE id = @id";

    [DatabaseFact]
    public async Task ABatchNoReviewWasRecordedForIsNotAcceptedByAnyStatementEvenWithTheEventOfTheClosing()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var untouched = await IngestionWorld.OpenAsync(world);
        var sampled = await IngestionWorld.OpenAsync(world);
        _ = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, sampled, IngestionWorld.Person), TestContext.Current.CancellationToken);

        var withoutSample = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, 1, Accept, ("event", untouched), ("id", untouched)));
        var withoutReview = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, 1, Accept, ("event", sampled), ("id", sampled)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAfterAsync(AcceptedEvent, 1, Accept, ("event", sampled), ("id", sampled)));

        Assert.Equal((WithoutEvent, WithoutEvent, WithoutEvent), (withoutSample.SqlState, withoutReview.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task ABatchWhoseReviewFoundSomethingIsNeverAccepted()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        var reviews = new BatchReviews(world.DataSource, 1);
        _ = await reviews.DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        _ = await reviews.ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person, "name_not_treated"), TestContext.Current.CancellationToken);

        var accepted = await IngestionWorld.AcceptAsync(world, batch);

        Assert.Equal(BatchOutcome.AlreadyClosed, accepted.Outcome);
    }

    [DatabaseFact]
    public async Task TheSampleDrawnAndTheReviewerRecordedNeverChangeByAnyStatement()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        var reviews = new BatchReviews(world.DataSource, 1);
        _ = await reviews.DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        _ = await reviews.ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);

        var sample = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Rewrite, ("id", batch)));
        var reviewer = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Reviewer, ("id", batch)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Rewrite, ("id", batch)));

        Assert.Equal((OutsideTheRules, OutsideTheRules, OutsideTheRules), (sample.SqlState, reviewer.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task NoItemEntersABatchOnceItsSampleWasDrawn()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddItemAsync(world, batch));

        Assert.Equal(1L, await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.item_text WHERE batch_id = @id", ("id", batch)));
    }
}
