using Core.Persistence.Content.Rerun;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The queue of the rerun (0064): an item is in one open request at most, a repeated request queues nothing and leads to the open one, each item is taken by one worker, a hold that expires is
/// taken again and spends a try, the last try refuses the item, and a request to another version closes what was left of the open one as superseded.
/// </summary>
[Trait("Category", "Database")]
public sealed class SanitizationRerunsTests : IClassFixture<RerunDatabase>
{
    private const string Sanitized = "texto sanitizado sintetico zqx";
    private const string AnotherRequest = "INSERT INTO content.sanitization_rerun (domain_id, target_version, requested_by) SELECT r.domain_id, r.target_version, r.requested_by "
        + "FROM content.sanitization_rerun r JOIN content.sanitization_rerun_item q ON q.rerun_id = r.id WHERE q.item_id = @item";
    private const string TheItemAgain = "INSERT INTO content.sanitization_rerun_item (domain_id, rerun_id, item_id) "
        + "SELECT domain_id, (SELECT max(id) FROM content.sanitization_rerun), id FROM content.item_text WHERE id = @item";

    private readonly RerunDatabase database;

    public SanitizationRerunsTests(RerunDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task AnItemAskedTwiceIsQueuedOnce()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);

        var requests = await Task.WhenAll(rerun.RequestAsync(), rerun.RequestAsync(queue: rerun.OtherQueue()));

        var rerunId = Assert.Single(requests.Select(request => request.RerunId).Distinct());
        Assert.Equal(1L, (await rerun.ProgressAsync(rerunId!.Value)).Queued);
    }

    [DatabaseFact]
    public async Task RequestWithNothingNewLeadsToTheOpenRerun()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        var first = await rerun.RequestAsync();

        var second = await rerun.RequestAsync();

        Assert.Equal((RerunOutcome.AlreadyOpen, first.RerunId), (second.Outcome, second.RerunId));
    }

    [DatabaseFact]
    public async Task RequestWithNothingToRerunWritesNoRequest()
    {
        var rerun = await database.CaseAsync();

        var requested = await rerun.RequestAsync();

        Assert.Equal(new RerunResult(RerunOutcome.NothingToRerun), requested);
        Assert.Null((await rerun.Queue.SituationAsync(rerun.DomainId, TestContext.Current.CancellationToken)).OpenRerunId);
    }

    [DatabaseFact]
    public async Task SituationCountsTheItemsOfTheAcceptedBatchesByVersionAndNamesTheOpenRerun()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        _ = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();

        var situation = await rerun.Queue.SituationAsync(rerun.DomainId, TestContext.Current.CancellationToken);

        Assert.Equal([new VersionItems(ItemTexts.Version, 2)], situation.Versions);
        Assert.Equal((requested.RerunId, RerunCase.Target), (situation.OpenRerunId, situation.OpenVersion));
    }

    [DatabaseFact]
    public async Task TwoWorkersTakeEachQueuedItemOnce()
    {
        var rerun = await database.CaseAsync();
        long[] items = [await rerun.ItemAsync(Sanitized), await rerun.ItemAsync(Sanitized)];
        _ = await rerun.RequestAsync();

        var holds = await Task.WhenAll(
            rerun.Queue.TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken),
            rerun.OtherQueue().TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken));

        Assert.Equal(items, holds.Select(hold => Assert.NotNull(hold).ItemId).Order());
        Assert.Null(await rerun.Queue.TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task ExpiredHoldIsTakenAgain()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();
        _ = await rerun.TakenAsync();
        rerun.Clock.Advance(SanitizationReruns.Hold + TimeSpan.FromSeconds(1));

        var again = Assert.NotNull(await rerun.OtherQueue().TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken));

        Assert.Equal((item, 1), (again.ItemId, again.Tries));
    }

    [DatabaseFact]
    public async Task HoldThatExpiresAtTheLastTryIsRefused()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();
        for (var hold = 0; hold < SanitizationReruns.Tries; hold++)
        {
            _ = await rerun.TakenAsync();
            rerun.Clock.Advance(SanitizationReruns.Hold + TimeSpan.FromSeconds(1));
        }

        var none = await rerun.Queue.TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken);

        Assert.Null(none);
        Assert.Equal([new RerunItemRefusal(item, rerun.DomainId, RerunRefusal.Failed)], (await rerun.ProgressAsync(requested.RerunId!.Value)).Refusals);
        Assert.Equal($"item_text,item_resanitized,sanitization_refused,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task ItemThatKeepsFailingIsRefusedAtTheLastTry()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();
        var failures = new List<RerunOutcome>();

        for (var attempt = 0; attempt < SanitizationReruns.Tries; attempt++)
        {
            failures.Add(await rerun.Queue.FailAsync(await rerun.TakenAsync(), TestContext.Current.CancellationToken));
        }

        Assert.Equal([RerunOutcome.Failed, RerunOutcome.Failed, RerunOutcome.Refused], failures);
        Assert.Equal([new RerunItemRefusal(item, rerun.DomainId, RerunRefusal.Failed)], (await rerun.ProgressAsync(requested.RerunId!.Value)).Refusals);
        Assert.Equal($"item_text,item_resanitized,sanitization_refused,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task ReturnedItemIsTakenAgainWithoutSpendingATry()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        Assert.Equal(RerunOutcome.Returned, await rerun.Queue.ReturnAsync(await rerun.TakenAsync(), TestContext.Current.CancellationToken));

        var again = await rerun.TakenAsync();
        Assert.Equal((item, 0), (again.ItemId, again.Tries));
    }

    [DatabaseFact]
    public async Task NewRequestClosesTheOpenRerunOfAnotherTargetAsSuperseded()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var first = await rerun.RequestAsync();

        var later = await rerun.RequestAsync(RerunCase.LaterSteps);

        var (closed, open) = (await rerun.ProgressAsync(first.RerunId!.Value), await rerun.ProgressAsync(later.RerunId!.Value));
        Assert.Equal((1L, 0L, 1L), (closed.Superseded, closed.Queued, open.Queued));
        Assert.Null(await rerun.Queue.TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken));
        Assert.Equal($"item_text,item_resanitized,sanitization_superseded,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task RequestEventRecordsWhoAskedAndTheTargetVersion()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);

        var requested = await rerun.RequestAsync();

        Assert.Equal($"sanitization_rerun,sanitization_rerun,administrative_act,{RerunCase.Target},zqx-curator", await rerun.RequestEventsOfAsync(requested.RerunId!.Value));
    }

    [DatabaseFact]
    public async Task TheDatabaseKeepsAnItemInOneOpenRequestAtMost()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.AsApplicationAsync(AnotherRequest, TheItemAgain, item));

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "sanitization_rerun_item_open"), (refused.SqlState, refused.ConstraintName));
    }
}
