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
    private static readonly string RequestEvent = $"sanitization_rerun,sanitization_rerun,administrative_act,{RerunCase.Target},zqx-curator";

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

        // The one that loses the race may give up after its tries, writing nothing; the item is still queued once, in the one open request.
        var rerunId = Assert.Single(requests.Select(request => request.RerunId).OfType<long>().Distinct());
        RerunResult[] answers = [new(RerunOutcome.Queued, rerunId), new(RerunOutcome.AlreadyOpen, rerunId), new(RerunOutcome.NotRecorded)];
        Assert.Contains(answers[0], requests);
        Assert.All(requests, request => Assert.Contains(request, answers));
        var situation = await rerun.Queue.SituationAsync(rerun.DomainId, TestContext.Current.CancellationToken);
        Assert.Equal((1L, rerunId), ((await rerun.ProgressAsync(rerunId)).Queued, situation.OpenRerunId));
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

    // A batch still open at the request, accepted after it: the new confirmation queues its item in the open request, never a second request the situation would hide.
    [DatabaseFact]
    public async Task RequestWithANewItemQueuesItInTheOpenRerun()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        var first = await rerun.RequestAsync();
        _ = await rerun.ItemAsync(Sanitized);

        var second = await rerun.RequestAsync();

        var situation = await rerun.Queue.SituationAsync(rerun.DomainId, TestContext.Current.CancellationToken);
        var progress = await rerun.ProgressAsync(first.RerunId!.Value);
        Assert.Equal((RerunOutcome.AlreadyOpen, first.RerunId, first.RerunId, 2L), (second.Outcome, second.RerunId, situation.OpenRerunId, progress.Queued));
        Assert.Equal($"{RequestEvent};{RequestEvent}", await rerun.RequestEventsOfAsync(first.RerunId.Value));
    }

    // An item the open request refused waits for the request to conclude: queuing it again in the same request would break the queue for every later confirmation.
    [DatabaseFact]
    public async Task ItemRefusedByTheOpenRerunIsNotQueuedAgainInIt()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        _ = await rerun.ItemAsync(Sanitized);
        var first = await rerun.RequestAsync();
        for (var attempt = 0; attempt < SanitizationReruns.Tries; attempt++)
        {
            _ = await rerun.Queue.FailAsync(await rerun.TakenAsync(), TestContext.Current.CancellationToken);
        }

        var second = await rerun.RequestAsync();

        var progress = await rerun.ProgressAsync(first.RerunId!.Value);
        Assert.Equal((RerunOutcome.AlreadyOpen, first.RerunId, 1L, 1L), (second.Outcome, second.RerunId, progress.Queued, progress.Refused));
        Assert.Equal(RequestEvent, await rerun.RequestEventsOfAsync(first.RerunId.Value));
    }

    [DatabaseFact]
    public async Task NewItemJoinsTheOpenRerunBesideAnItemItRefused()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        _ = await rerun.ItemAsync(Sanitized);
        var first = await rerun.RequestAsync();
        for (var attempt = 0; attempt < SanitizationReruns.Tries; attempt++)
        {
            _ = await rerun.Queue.FailAsync(await rerun.TakenAsync(), TestContext.Current.CancellationToken);
        }

        _ = await rerun.ItemAsync(Sanitized);
        var second = await rerun.RequestAsync();

        var progress = await rerun.ProgressAsync(first.RerunId!.Value);
        Assert.Equal((RerunOutcome.AlreadyOpen, first.RerunId, 2L, 1L), (second.Outcome, second.RerunId, progress.Queued, progress.Refused));
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
        var items = new[] { await rerun.ItemAsync(Sanitized), await rerun.ItemAsync(Sanitized) };
        var first = await rerun.RequestAsync();

        var later = await rerun.RequestAsync(RerunCase.LaterSteps);

        var (closed, open) = (await rerun.ProgressAsync(first.RerunId!.Value), await rerun.ProgressAsync(later.RerunId!.Value));
        Assert.Equal((2L, 0L, 2L), (closed.Superseded, closed.Queued, open.Queued));
        Assert.Null(await rerun.Queue.TakeAsync(rerun.DomainId, RerunCase.Target, TestContext.Current.CancellationToken));
        foreach (var item in items)
        {
            Assert.Equal($"item_text,item_resanitized,sanitization_superseded,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
        }
    }

    [DatabaseFact]
    public async Task RequestEventRecordsWhoAskedAndTheTargetVersion()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);

        var requested = await rerun.RequestAsync();

        Assert.Equal(RequestEvent, await rerun.RequestEventsOfAsync(requested.RerunId!.Value));
    }

    // The chain answers every try of the request with a conflict: the request gives up after its three tries and records nothing.
    [DatabaseFact]
    public async Task RequestThatMeetsAConflictAtEveryTryGivesUpAfterThreeAndRecordsNothing()
    {
        await using var own = new RerunDatabase();
        var rerun = await own.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        await rerun.RequestsAlwaysConflictAsync();

        var result = await rerun.RequestAsync();

        Assert.Equal((RerunOutcome.NotRecorded, 3L), (result.Outcome, await rerun.ConflictedRequestsAsync()));
    }

    [DatabaseFact]
    public async Task ARequestThatMeetsTheItemQueuedByAnotherAtTheSameTimeTriesAgainAndQueuesIt()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var (elsewhere, other) = await rerun.QueuedElsewhereAsync(item);
        RerunResult result;
        await using (elsewhere)
        {
            var asked = rerun.RequestAsync();
            await rerun.UntilSomeoneWaitsAsync(asked);

            await elsewhere.CommitAsync(TestContext.Current.CancellationToken);
            result = await asked;
        }

        Assert.Equal(RerunOutcome.Queued, result.Outcome);
        Assert.Equal((1L, 1L), ((await rerun.ProgressAsync(result.RerunId!.Value)).Queued, (await rerun.ProgressAsync(other)).Superseded));
    }

    // Another writer takes the link of the chain while the request waits to supersede: the event of the first item is a conflict, the attempt stops there, and the next one records both.
    [DatabaseFact]
    public async Task RequestWhoseEventOfSupersedingMeetsAnotherWriterStopsThereAndTriesAgain()
    {
        var rerun = await database.CaseAsync();
        var items = new[] { await rerun.ItemAsync(Sanitized), await rerun.ItemAsync(Sanitized) };
        var first = await rerun.RequestAsync();
        RerunResult later;
        await using (var locked = await rerun.QueueLockedAsync())
        {
            var asked = rerun.RequestAsync(RerunCase.LaterSteps);
            await rerun.UntilSomeoneWaitsAsync(asked);

            rerun.AppendElsewhere();
            await locked.CommitAsync(TestContext.Current.CancellationToken);
            later = await asked;
        }

        Assert.Equal((RerunOutcome.Queued, 2L), (later.Outcome, (await rerun.ProgressAsync(first.RerunId!.Value)).Superseded));
        foreach (var item in items)
        {
            Assert.Equal($"item_text,item_resanitized,sanitization_superseded,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
        }
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
