using Core.Boundary.Contracts;
using Core.Host.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging;

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

        Assert.Equal((SubmissionKind.Unavailable, SubmissionStoring.SubmissionCancelled, BatchState.Rejected, SubmissionStoring.SubmissionCancelled, 0L),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task EveryItemKeptIsBornWithItsSanitizedText()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Seeded));

        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        Assert.Equal(
            ["3", "0"],
            [.. await world.StoredAsync("SELECT count(*)::text FROM content.item_text"), .. await world.StoredAsync("SELECT count(*)::text FROM content.item_text WHERE sanitized_text IS NULL")]);
    }

    [DatabaseFact]
    public async Task TheBatchRecordsTheVersionOfTheStepsByWhoeverSubmittedIt()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: WholeSeed());

        _ = await world.SubmitAsync(SubmissionWorld.Request(Seeded));

        Assert.Equal(
            [$"{DeclaredSanitization.Sanitizer.Version},{SubmissionWorld.Person}"],
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
        Assert.Equal(["(" + DeclaredSanitization.Sanitizer.Version + ",destinos,3,3)"], report);
        string[] swept =
        [
            .. world.Log.Lines.Select(entry => entry.Line),
            .. report,
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

        Assert.Equal(DeclaredSanitization.Sanitizer.Version, counts.SanitizationVersion);
        Assert.Equal([new BatchStepCount("destinos", 3, 3)], counts.Steps);
    }

    /// <summary>A recognizer that finds, in every text, one credential that covers it whole: the seeded texts are all of the same length.</summary>
    private static Analysis.AnsweringRecognizer WholeSeed() =>
        new(RecognitionOutcome.Analyzed(new AnalysisResponse([new Occurrence(0, "ZQXSEMENTE01".Length, "CREDENTIAL", "PROPN", "nsubj")], "zqx-model-0027")));
}
