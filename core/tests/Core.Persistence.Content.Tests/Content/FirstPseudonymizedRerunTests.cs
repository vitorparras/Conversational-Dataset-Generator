using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The first pseudonymized text of an item stored before the coreference, through the rerun (issue 102, F3): the item that holds no map is given the raw texts of its batch, gets its
/// pseudonymized text and a map of its own in the transaction of the rerun, and its markers are won for its batch; two such items of one batch never share a marker, the second being
/// drawn again without it, and a request to the version in force queues an old item already in it; an item refused because an occurrence has no pair keeps no pseudonymized text, is
/// counted with the reason and is queued again by a new request.
/// </summary>
[Trait("Category", "Database")]
public sealed class FirstPseudonymizedRerunTests : IClassFixture<RerunDatabase>
{
    private const string Sanitized = "texto sanitizado antigo zqx";
    private const string NewText = "texto sanitizado de novo zqx";
    private const string OtherRaw = "outro texto bruto zqx";

    private readonly RerunDatabase database;

    public FirstPseudonymizedRerunTests(RerunDatabase database)
    {
        this.database = database;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task ItemWithoutPseudonymizedGetsItsFirstOne()
    {
        var rerun = await database.CaseAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.ItemAsync(Sanitized, raw: OtherRaw);
        await rerun.EmptyAsync(await rerun.ItemAsync(Sanitized));
        _ = await rerun.ItemAsync(Sanitized, await rerun.AnotherBatchAsync());
        _ = await rerun.RequestAsync();
        var seen = new List<RerunSource>();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), First(_ => marker, seen.Add), Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal((PseudonymizedTexts.PseudonymizedOf([marker]), NewText), (await rerun.PseudonymizedTextAsync(item), await rerun.SanitizedAsync(item)));
        Assert.Equal(("1,1", $"{marker}:false|{marker}"), (await rerun.MapsOfAsync(item), await rerun.MarkersOfAsync(item)));
        var source = Assert.Single(seen);
        Assert.Equal([RerunCase.Raw, OtherRaw], source.Batch);
        Assert.Equal((0, true), (source.Position, source.Map is null));
    }

    [DatabaseFact]
    public async Task OldItemWhoseSanitizedComesOutTheSameGetsItsFirstOneAsWell()
    {
        var rerun = await database.CaseAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), First(_ => marker, _ => { }, Sanitized), Token);

        Assert.Equal(RerunOutcome.Identical, result.Outcome);
        Assert.Equal((PseudonymizedTexts.PseudonymizedOf([marker]), Sanitized, "1,1"), (await rerun.PseudonymizedTextAsync(item), await rerun.SanitizedAsync(item), await rerun.MapsOfAsync(item)));
        Assert.Equal(RerunCase.Target, await rerun.VersionAsync());
    }

    [DatabaseFact]
    public async Task OldItemAlreadyInTheVersionInForceGetsItsFirstOne()
    {
        var rerun = await database.CaseAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var item = await rerun.ItemAsync(Sanitized);

        var requested = await rerun.RequestAsync(ItemTexts.Steps);
        var hold = Assert.NotNull(await rerun.Queue.TakeAsync(rerun.DomainId, ItemTexts.Version, Token));
        var result = await rerun.Resanitizer.ResanitizeAsync(hold, First(_ => marker, _ => { }, Sanitized, ItemTexts.Version), Token);

        Assert.Equal((RerunOutcome.Queued, RerunOutcome.Identical), (requested.Outcome, result.Outcome));
        Assert.Equal((PseudonymizedTexts.PseudonymizedOf([marker]), "1,1"), (await rerun.PseudonymizedTextAsync(item), await rerun.MapsOfAsync(item)));
        Assert.Equal(ItemTexts.Version, await rerun.VersionAsync());
        Assert.Equal(new RerunResult(RerunOutcome.NothingToRerun), await rerun.RequestAsync(ItemTexts.Steps));
    }

    [DatabaseFact]
    public async Task OldItemTheServiceDoesNotAnswerSpendsATryAndKeepsNoPseudonymized()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), (_, _) => Task.FromResult(RerunAnswer.Unanswered), Token);

        Assert.Equal((RerunOutcome.Failed, null), (result.Outcome, result.Refusal));
        Assert.Equal((null, "0,0"), (await rerun.PseudonymizedTextAsync(item), await rerun.MapsOfAsync(item)));
    }

    [DatabaseFact]
    public async Task OldItemsOfABatchNeverShareAMarker()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (shared, own) = (markers[0], markers[1]);
        var first = await rerun.ItemAsync(Sanitized);
        var second = await rerun.ItemAsync(Sanitized);
        _ = await rerun.RequestAsync();
        var seen = new List<RerunSource>();
        var drawing = First(source => source.Taken.Contains(shared) ? own : shared, seen.Add);

        _ = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), drawing, Token);
        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), drawing, Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal(($"{shared}:false|{Joined(shared, own)}", $"{own}:false|{Joined(shared, own)}"), (await rerun.MarkersOfAsync(first), await rerun.MarkersOfAsync(second)));
        Assert.Equal(PseudonymizedTexts.PseudonymizedOf([own]), await rerun.PseudonymizedTextAsync(second));
        Assert.Equal([(0, false), (1, false), (1, true)], seen.Select(source => (source.Position, source.Taken.Contains(shared))));
    }

    [DatabaseFact]
    public async Task OldItemWithAnOccurrenceWithoutPairIsRefusedWithTheReason()
    {
        var rerun = await database.CaseAsync();
        var item = await rerun.ItemAsync(Sanitized);
        var requested = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(
            await rerun.TakenAsync(), (_, _) => Task.FromResult(new RerunAnswer(null, RerunRefusal.PseudonymWithoutPair)), Token);

        Assert.Equal((RerunOutcome.Refused, RerunRefusal.PseudonymWithoutPair), (result.Outcome, result.Refusal));
        Assert.Equal([new RerunItemRefusal(item, rerun.DomainId, RerunRefusal.PseudonymWithoutPair)], (await rerun.ProgressAsync(requested.RerunId!.Value)).Refusals);
        Assert.Equal((null, Sanitized, "0,0"), (await rerun.PseudonymizedTextAsync(item), await rerun.SanitizedAsync(item), await rerun.MapsOfAsync(item)));
        var again = await rerun.RequestAsync(RerunCase.LaterSteps);
        Assert.Equal((RerunOutcome.Queued, 1L), (again.Outcome, (await rerun.ProgressAsync(again.RerunId!.Value)).Queued));
    }

    /// <summary>
    /// The rerun to the text given, by default <see cref="NewText"/>, and to the version given, by default the target, that gives the item its first pseudonymized text, with the one
    /// marker the function picks for what it is given, keeping each source given.
    /// </summary>
    private static Func<RerunSource, Resanitized> First(Func<RerunSource, string> marker, Action<RerunSource> seen, string text = NewText, string? version = null) => source =>
    {
        seen(source);
        var picked = marker(source);
        return RerunCase.Rerunning(text, version)(source) with
        {
            Pseudonymized = PseudonymizedTexts.PseudonymizedOf([picked]),
            Map = [new NewMapEntry(picked, PseudonymizedTexts.OriginalOf(picked), false, OccurrenceDestination.Pseudonymize)],
        };
    };

    private static string Joined(params string[] values) => string.Join(',', values.Order(StringComparer.Ordinal));
}
