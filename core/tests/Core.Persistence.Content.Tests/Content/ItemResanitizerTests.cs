using Core.Persistence.Content.Rerun;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The rerun of one item over its raw text (issue 97): in one transaction the new sanitization, the event item_resanitized and the conclusion in the queue go together or none goes; an identical
/// text is not written again; an emptied item, or one whose key was declared lost, is refused with the reason; and the database refuses a change of the sanitization without the rerun (0064).
/// </summary>
[Trait("Category", "Database")]
public sealed class ItemResanitizerTests : IClassFixture<RerunDatabase>
{
    private const string Sanitized = "texto sanitizado sintetico zqx";
    private const string Resanitized = "texto sanitizado de novo zqx";
    private const string KeptOutsideTheRerun = "CE004";
    private const string ChangeCounts = "UPDATE content.item_text SET sanitization_counts = '{4}' WHERE id = @item";
    private const string RerunEvent = "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (true, 'item_text', @item, 'item_resanitized', 'sanitization_replaced', '49ca4d5854062508ba965bbec6169a7898d926b3570c4deae45250f5a36d8700')";

    private const string ConcludeInTheQueue =
        "UPDATE content.sanitization_rerun_item SET state = 'done', hold_until = NULL, result = 'replaced', concluded_at = now() WHERE item_id = @item AND state <> 'done'";

    private readonly RerunDatabase database;

    public ItemResanitizerTests(RerunDatabase database)
    {
        this.database = database;
    }

    // The queue takes only an item whose version is not the target, so the rerun of the same version is the domain answering the text and the version the item holds: nothing of it is written.
    [DatabaseFact]
    public async Task RerunWithTheSameVersionIsReportedIdenticalAndNotRewritten()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();
        var before = await rerun.SanitizationOfAsync(item);

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Sanitized, ItemTexts.Version), TestContext.Current.CancellationToken);

        Assert.Equal(RerunOutcome.Identical, result.Outcome);
        Assert.Equal(before, await rerun.SanitizationOfAsync(item));
    }

    [DatabaseFact]
    public async Task IdenticalTextUnderANewVersionKeepsTheSanitizedTextAndRecordsTheVersion()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Sanitized), TestContext.Current.CancellationToken);

        Assert.Equal((RerunOutcome.Identical, Sanitized, RerunCase.Target), (result.Outcome, await rerun.SanitizedAsync(item), await rerun.VersionAsync()));
    }

    [DatabaseFact]
    public async Task RerunOfAnEmptiedItemIsRefusedWithTheReason()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();
        await rerun.EmptyAsync(item);

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Resanitized), TestContext.Current.CancellationToken);

        Assert.Equal((RerunOutcome.Refused, RerunRefusal.Emptied), (result.Outcome, result.Refusal));
        Assert.Equal([new RerunItemRefusal(item, rerun.DomainId, RerunRefusal.Emptied)], (await rerun.ProgressAsync(requested.RerunId!.Value)).Refusals);
        Assert.Equal($"item_text,item_resanitized,sanitization_refused,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task TheRerunIsGivenTheRawTextAndTheSanitizedTextTheItemHolds()
    {
        var rerun = await database.CaseAsync();
        _ = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();
        var given = new List<RerunSource>();

        _ = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), source =>
        {
            given.Add(source);
            return RerunCase.Rerunning(Resanitized)(source);
        }, TestContext.Current.CancellationToken);

        var source = Assert.Single(given);
        Assert.Equal((RerunCase.Raw, Sanitized, ItemTexts.Version), (source.Raw, source.StoredText, source.StoredVersion));
    }

    [DatabaseFact]
    public async Task RerunOfAnItemWithoutDecidedDestinationsIsRefusedWithTheReason()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.UndecidedItemAsync();
        var requested = await rerun.RequestAsync();
        var before = await rerun.SanitizationOfAsync(item);
        var given = new List<RerunSource>();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), source =>
        {
            given.Add(source);
            return null;
        }, TestContext.Current.CancellationToken);

        Assert.Equal((RerunOutcome.Refused, RerunRefusal.Undecided), (result.Outcome, result.Refusal));
        Assert.Null(Assert.Single(given).Occurrences);
        Assert.Equal([new RerunItemRefusal(item, rerun.DomainId, RerunRefusal.Undecided)], (await rerun.ProgressAsync(requested.RerunId!.Value)).Refusals);
        Assert.Equal(before, await rerun.SanitizationOfAsync(item));
        Assert.Equal($"item_text,item_resanitized,sanitization_refused,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task RerunOfAnItemWithLostKeyIsRefusedWithTheReason()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();
        await rerun.DeclareKeyLossAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Resanitized), TestContext.Current.CancellationToken);

        Assert.Equal((RerunOutcome.Refused, RerunRefusal.KeyLost), (result.Outcome, result.Refusal));
        Assert.Equal([new RerunItemRefusal(item, rerun.DomainId, RerunRefusal.KeyLost)], (await rerun.ProgressAsync(requested.RerunId!.Value)).Refusals);
        Assert.Equal($"item_text,item_resanitized,sanitization_refused,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task NewVersionReplacesTheTextAndRecordsTheVersion()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Resanitized), TestContext.Current.CancellationToken);

        Assert.Equal((RerunOutcome.Replaced, Resanitized, RerunCase.Target), (result.Outcome, await rerun.SanitizedAsync(item), await rerun.VersionAsync()));
    }

    [DatabaseFact]
    public async Task InterruptedRerunLeavesNoItemWithTextOfOneVersionAndRecordOfAnother()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();
        var hold = await rerun.TakenAsync();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rerun.Resanitizer.ResanitizeAsync(hold, _ => throw new InvalidOperationException("The rerun stopped."), TestContext.Current.CancellationToken));

        Assert.Equal((Sanitized, ItemTexts.Version, string.Empty), (await rerun.SanitizedAsync(item), await rerun.VersionAsync(), await rerun.RerunEventsOfAsync(item)));
        var progress = await rerun.ProgressAsync(requested.RerunId!.Value);
        Assert.Equal((1L, 0L), (progress.Held, progress.Replaced));
    }

    [DatabaseFact]
    public async Task RerunEventRecordsWhatWithWhichVersionAndResult()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        _ = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Resanitized), TestContext.Current.CancellationToken);

        Assert.Equal($"item_text,item_resanitized,sanitization_replaced,{RerunCase.Target},true", await rerun.RerunEventsOfAsync(item));
    }

    [DatabaseFact]
    public async Task ResanitizingWithoutTheEventIsRefused()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.AsApplicationAsync(string.Empty, ChangeCounts, item));

        Assert.Equal(KeptOutsideTheRerun, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ResanitizingWithTheConclusionInTheQueueButNoEventIsRefused()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.AsApplicationAsync(ConcludeInTheQueue, ChangeCounts, item));

        Assert.Equal(KeptOutsideTheRerun, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ResanitizingWithTheEventAndTheItemOnlyQueuedIsRefused()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.AsApplicationAsync(RerunEvent, ChangeCounts, item));

        Assert.Equal(KeptOutsideTheRerun, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ResanitizingWithTheEventAndAConclusionOfAnotherTransactionIsRefused()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();
        _ = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), RerunCase.Rerunning(Resanitized), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.AsApplicationAsync(RerunEvent, ChangeCounts, item));

        Assert.Equal(KeptOutsideTheRerun, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ResanitizingWithTheEventButNoConclusionInTheQueueIsRefused()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.AsApplicationAsync(RerunEvent, ChangeCounts, item));

        Assert.Equal(KeptOutsideTheRerun, refused.SqlState);
    }

    [DatabaseFact]
    public async Task SerializationConflictReturnsTheItemWithoutSpendingATry()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();
        var conflicting = RerunCase.Rerunning(Resanitized);

        Resanitized AfterAnotherWriter(RerunSource source)
        {
            rerun.AppendElsewhere();
            return conflicting(source);
        }

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), AfterAnotherWriter, TestContext.Current.CancellationToken);

        var again = await rerun.TakenAsync();
        Assert.Equal((RerunOutcome.Returned, item, 0), (result.Outcome, again.ItemId, again.Tries));
    }
}
