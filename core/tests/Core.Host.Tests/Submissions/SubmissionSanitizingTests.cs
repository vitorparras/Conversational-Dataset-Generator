using System.Globalization;
using Core.Boundary.Contracts;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The submission sanitizes every text of the material before it writes the first item (#842, issues 87, 92 and 93): every item is born with its sanitized text and the version of the steps,
/// the batch records that version in the chain, and a step that fails or a submission cancelled leaves no item. Neither the log nor the report carries a word of the material.
/// </summary>
public sealed class SubmissionSanitizingTests
{
    private const string SanitizationFailed = "sanitization_failed";
    private const string Seed = "SEMENTE";

    /// <summary>Three texts of the same length, each one a value seeded to be recognized whole.</summary>
    private static readonly string Seeded = SubmissionWorld.Messages(("ZQXUM", "ZQXSEMENTE01"), ("ZQXDOIS", "ZQXSEMENTE02"), ("ZQXUM", "ZQXSEMENTE03"));

    private static readonly int[] Granted = [SubmissionWorld.Domain];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task StepFailureLogCarriesNoMaterialText()
    {
        var step = SanitizingStep.FailingAt(2);
        await using var world = await SubmissionWorld.CreateAsync(sanitizer: step.Alone());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal((SubmissionKind.Unavailable, SanitizationFailed, BatchState.Rejected, SanitizationFailed, 0L, 2),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync(), step.Calls));
        var line = Assert.Single(world.Log.Lines, entry => entry.EventId == 351);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("passo-de-teste", line.Line, StringComparison.Ordinal);
        Assert.Contains("Position=1", line.Line, StringComparison.Ordinal);
        Assert.Contains($"Batch={outcome.Batch.Value}", line.Line, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), line.Line, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX", line.Line, StringComparison.Ordinal);
        Assert.DoesNotContain("falha sintética", line.Line, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task CancelledWhileSanitizingLeavesNoItemWithoutSanitized()
    {
        using var cancelling = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var step = SanitizingStep.CancellingAt(2, cancelling);
        await using var world = await SubmissionWorld.CreateAsync(sanitizer: step.Alone());

        var outcome = await world.Service.SubmitAsync(SubmissionWorld.Request(Seeded), cancelling.Token);
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal((SubmissionKind.Unavailable, SubmissionStoring.SubmissionCancelled, BatchState.Rejected, SubmissionStoring.SubmissionCancelled, 0L, 2),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync(), step.Calls));
    }

    [DatabaseFact]
    public async Task EveryItemKeptIsBornWithItsSanitizedText()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));

        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        Assert.Equal(["[credencial]", "[credencial]", "[credencial]"], await OpenedAsync(world, ContentColumns.SanitizedText));
    }

    [DatabaseFact]
    public async Task TheBatchRecordsTheVersionOfTheStepsByWhoeverSubmittedIt()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());

        _ = await world.SubmitAsync(SubmissionWorld.Request(Seeded));

        Assert.Equal(
            [$"{CompositionRoot.StepsOf().Version},{SubmissionWorld.Person}"],
            await world.Database.FirstColumnAsOwnerAsync("SELECT reference || ',' || coalesce(actor_person_id::text, '-') FROM public.custody_event WHERE step = 'batch_sanitized'"));
    }

    [DatabaseFact]
    public async Task ReportAndLogsOfASeededBatchCarryNoSeededValue()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));

        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        var report = await world.StoredAsync(
            $"SELECT f::text FROM public.batch_sanitization_counts({outcome.Batch!.Value}, {SubmissionWorld.Domain}) f");
        var version = CompositionRoot.StepsOf().Version;
        Assert.Equal([$"({version},codificacao,0,0)", $"({version},artefatos,0,0)", $"({version},identificadores,0,0)", $"({version},destinos,3,3)"], report);
        string[] swept =
        [
            .. world.Log.Lines.Select(entry => entry.Line),
            .. report,
            .. await OpenedAsync(world, ContentColumns.SanitizedText),
            .. await OpenedAsync(world, ContentColumns.SanitizationTrail),
            .. await world.StoredAsync("SELECT t::text FROM content.item_text t"),
            .. await world.StoredAsync("SELECT v::text FROM content.sanitization_version v"),
            .. await world.Database.FirstColumnAsOwnerAsync("SELECT e::text FROM public.custody_event e"),
        ];
        Assert.DoesNotContain(swept, written => written.Contains(Seed, StringComparison.OrdinalIgnoreCase));
    }

    [DatabaseFact]
    public async Task TheCountsOfTheBatchReadTheVersionAndTheEditsOfEachStep()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));
        var counts = await new BatchCounts(world.DataSource).ReadAsync(SubmissionWorld.Domain, outcome.Batch!.Value, Token);

        Assert.Equal(CompositionRoot.StepsOf().Version, counts.SanitizationVersion);
        Assert.Equal([new BatchStepCount("codificacao", 0, 0), new BatchStepCount("artefatos", 0, 0), new BatchStepCount("identificadores", 0, 0), new BatchStepCount("destinos", 3, 3)], counts.Steps);
    }

    [DatabaseFact]
    public async Task RecordOfTheStepsThatMeetsAnotherEventOfTheChainIsACollisionAndNotAWriteThatFailed()
    {
        await using var world = await SubmissionWorld.CreateAsync(attempts: 1, recognizer: WholeSeed());
        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));
        var material = new ReadMaterial([new ReadUnit("ZQXUM", "ZQXREMETENTE", new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero), null, [])]);
        var accepted = SubmissionWorld.AcceptedAs(MaterialForm.Conversation, material);
        var kept = new SubmissionStoring.Kept(SubmissionWorld.Domain, outcome.Batch!.Value, material, MaterialForm.Conversation, accepted, SubmissionWorld.Person);

        var recorded = await SubmissionContention.WhilePendingAsync(world, () => SubmissionCleaning.RecordedAsync(world.Batches, CompositionRoot.StepsOf().Version, kept, Token));

        Assert.Equal(SubmissionStoring.Keeping.Collided, recorded);
    }

    [DatabaseFact]
    public async Task RecordOfTheStepsRefusedUnreachableOrCancelledIsNeverKept()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());
        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));
        var material = new ReadMaterial([new ReadUnit("ZQXUM", "ZQXREMETENTE", new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero), null, [])]);
        var accepted = SubmissionWorld.AcceptedAs(MaterialForm.Conversation, material);
        SubmissionStoring.Kept By(long person) => new(SubmissionWorld.Domain, outcome.Batch!.Value, material, MaterialForm.Conversation, accepted, person);
        var version = CompositionRoot.StepsOf().Version;
        var conditions = OriginConditions.Of([SubmissionWorld.Loosest]);
        await using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=zqx;Username=zqx;Password=zqx;Timeout=2");
        var closed = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=zqx;Username=zqx;Password=zqx;Timeout=2");
        await closed.DisposeAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        SubmissionStoring.Keeping[] recorded =
        [
            await SubmissionCleaning.RecordedAsync(world.Batches, version, By(9_000_001), Token),
            await SubmissionCleaning.RecordedAsync(new IngestionBatches(closed, conditions, 1), version, By(SubmissionWorld.Person), Token),
            await SubmissionCleaning.RecordedAsync(new IngestionBatches(unreachable, conditions, 1), version, By(SubmissionWorld.Person), Token),
            await SubmissionCleaning.RecordedAsync(world.Batches, version, By(SubmissionWorld.Person), cancelled.Token),
        ];

        Assert.Equal(
            [SubmissionStoring.Keeping.WriteFailed, SubmissionStoring.Keeping.WriteFailed, SubmissionStoring.Keeping.WriteFailed, SubmissionStoring.Keeping.Cancelled], recorded);
        Assert.Equal(["1"], await world.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.custody_event WHERE step = 'batch_sanitized'"));
    }

    /// <summary>The column given of every item, opened with the key of the item as the reader opens it, in the order the items were written.</summary>
    private static async Task<string[]> OpenedAsync(SubmissionWorld world, ProtectedColumn column)
    {
        var rows = await world.StoredAsync(
            $"SELECT encode({column.Name}, 'base64') || ',' || dek_epoch || ',' || encode(dek_wrapped, 'base64') FROM content.item_text ORDER BY id");
        return [.. rows.Select(row => row.Split(',')).Select(parts => world.Content.Open(
            column, SubmissionWorld.Domain, Convert.FromBase64String(parts[0]), new ContentKey(int.Parse(parts[1], CultureInfo.InvariantCulture), Convert.FromBase64String(parts[2]))))];
    }

    /// <summary>A recognizer that finds, in every text, one credential that covers it whole: the seeded texts are all of the same length.</summary>
    private static Analysis.AnsweringRecognizer WholeSeed() =>
        new(RecognitionOutcome.Analyzed(new AnalysisResponse([new Occurrence(0, "ZQXSEMENTE01".Length, "CREDENTIAL", "PROPN", "nsubj")], "zqx-model-0027")));
}
