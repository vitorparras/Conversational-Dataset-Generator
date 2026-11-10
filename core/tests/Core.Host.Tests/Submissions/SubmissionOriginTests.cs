using Core.Host.Submissions;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Submissions;

/// <summary>The origin of a submission (issue 75, part 2b, decision 0025): required, sealed, read only with an event of the chain, and answered only as declared or unknown.</summary>
public sealed class SubmissionOriginTests
{
    [DatabaseFact]
    public async Task OriginDeclaredIsAnsweredOnlyAsDeclaredAndTheTextIsInNoAnswerAndNoPlaceOfTheDatabase()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal(("declared", "declared"), (outcome.Origin, entry!.OriginState));
        Assert.DoesNotContain(SubmissionWorld.Origin, $"{outcome}{entry}", StringComparison.Ordinal);
        Assert.DoesNotContain(SubmissionWorld.Origin, await world.Database.DumpAsync(), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OriginUnknownIsAValidChoiceAnsweredAsUnknownAndSealsNothing()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request() with { Origin = null, OriginUnknown = true });
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionKind.Done, "unknown", "unknown"), (outcome.Kind, outcome.Origin, entry!.OriginState));
        Assert.Equal(0L, await world.OriginRowsAsync());
    }

    [DatabaseFact]
    public async Task OriginMissingRecordsNothingAndReadsNoByteAndTheFieldIsNamed()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var request = SubmissionWorld.Request() with { Origin = "  " };

        var outcome = await world.SubmitAsync(request);

        Assert.Equal(
            (SubmissionKind.Invalid, "origin:missing", 0L, 0L),
            (outcome.Kind, string.Join(',', outcome.Fields!.Select(field => $"{field.Field}:{field.Code}")), request.Content.Position, await world.BatchCountAsync()));
    }

    [DatabaseTheory]
    [InlineData(true, "conflict")]
    [InlineData(false, "too_long")]
    public async Task OriginThatContradictsItselfOrIsTooLongIsRefusedAndRecordsNothing(bool alsoUnknown, string code)
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var text = alsoUnknown ? SubmissionWorld.Origin : new string('x', OriginDeclaration.MostCharacters + 1);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request() with { Origin = text, OriginUnknown = alsoUnknown });

        Assert.Equal(($"origin:{code}", 0L), (string.Join(',', outcome.Fields!.Select(field => $"{field.Field}:{field.Code}")), await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task SubmittingDoesNotReadTheOriginAndReadingItIsAnEventOfTheChainAboutTheBatch()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var before = string.Join(";", await world.OriginEventsAsync());

        var read = await world.Reader.ReadAsync(
            new ContentRead(CustodyAuthor.Person(SubmissionWorld.Person), ContentColumns.Origin, SubmissionWorld.Domain, outcome.Batch!.Value), TestContext.Current.CancellationToken);

        Assert.Equal(
            ("", ContentReadOutcome.Read, SubmissionWorld.Origin, $"ingestion_batch,{outcome.Batch},origin_read,content_read"),
            (before, read.Outcome, read.Text, string.Join(";", await world.OriginEventsAsync())));
    }

    [DatabaseFact]
    public async Task OriginOfARejectedBatchIsKeptWithItAndTheDomainWithoutAKeyIsAnOutcomeNotACrash()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var rejected = await world.SubmitAsync(SubmissionWorld.Request("nada"));
        var elsewhere = await world.SubmitAsync(SubmissionWorld.Request() with { DomainId = 99 });

        Assert.Equal(
            (SubmissionState.Rejected, "declared", 1L, SubmissionKind.NotRecorded),
            (rejected.State, rejected.Origin, await world.OriginRowsAsync(), elsewhere.Kind));
    }
}
