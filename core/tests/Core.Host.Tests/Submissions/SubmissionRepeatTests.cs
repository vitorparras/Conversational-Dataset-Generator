using System.Globalization;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The repeat of a submission and where an item came from (issue 80): a file is recognized as sent before by the SHA-256 of what came, in the same domain, and never by its name;
/// the repeat is registered as turned back with the code and names the batch that recognized it, and creates no item; and an item says which batch kept it, when it came and who sent it.
/// </summary>
public sealed class SubmissionRepeatTests
{
    private static readonly int[] Granted = [SubmissionWorld.Domain];

    [DatabaseFact]
    public async Task FileSentAgainIsRecognizedByTheDigestAndNamesTheBatchThatAcceptedItBeforeWithoutAnyNewItem()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var first = await world.SubmitAsync(SubmissionWorld.Request());
        await world.AcceptReviewedAsync(first.Batch!.Value);
        var again = await world.SubmitAsync(SubmissionWorld.Request());
        var detail = await world.Service.DetailAsync(Granted, again.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionState.Accepted, SubmissionState.Rejected, ReasonCodes.AlreadyReceived, first.Batch), (first.State, again.State, again.Reason!.Code, again.Reason.PreviousBatch));
        Assert.Equal((BatchState.Rejected, ReasonCodes.AlreadyReceived, first.Batch, first.Sha256), (detail!.State, detail.Reason, detail.PreviousBatch, detail.Receipt!.Digest));
        Assert.Equal((2L, 2L, 2L), (await world.ItemRowsAsync(), await world.BatchCountAsync(), await world.OriginRowsAsync()));
    }

    [DatabaseFact]
    public async Task TwoIdenticalFilesSentAtTheSameTimeBothStayOpenAndTheDatabaseLetsOnlyTheFirstToBeAcceptedHoldTheDigest()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var both = await Task.WhenAll(world.SubmitAsync(SubmissionWorld.Request()), world.SubmitAsync(SubmissionWorld.Request()));
        await world.AcceptReviewedAsync(both[0].Batch!.Value);
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, SubmissionWorld.Domain, both[1].Batch!.Value, SubmissionWorld.Person);
        var repeated = await world.Batches.AcceptAsync(new BatchClosing(SubmissionWorld.Domain, both[1].Batch!.Value, CustodyAuthor.System), TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionState.Accepted, SubmissionState.Accepted, BatchOutcome.AlreadyReceived, 2L), (both[0].State, both[1].State, repeated.Outcome, await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task SameContentUnderTwoDifferentNamesIsTheSameFileAndTheSameNameOverOtherContentIsNot()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var other = SubmissionWorld.Conversation.Replace("ZQXPRIMEIRO", "ZQXOUTRO", StringComparison.Ordinal);

        var first = await world.SubmitAsync(SubmissionWorld.Request(name: "um.json"));
        await world.AcceptReviewedAsync(first.Batch!.Value);
        var renamed = await world.SubmitAsync(SubmissionWorld.Request(name: "dois.json"));
        var changed = await world.SubmitAsync(SubmissionWorld.Request(content: other, name: "um.json"));

        Assert.Equal(
            (SubmissionState.Rejected, first.Batch, SubmissionState.Accepted, null),
            (renamed.State, renamed.Reason!.PreviousBatch, changed.State, changed.Reason?.PreviousBatch));
        Assert.NotEqual(first.Sha256, changed.Sha256);
    }

    [DatabaseFact]
    public async Task OnlyAFileAnotherBatchOfTheSameDomainAcceptedIsARepeat()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var turnedBack = SubmissionWorld.Request() with { DeclaredForm = MaterialForm.Comment };

        var refused = await world.SubmitAsync(turnedBack);
        var accepted = await world.SubmitAsync(SubmissionWorld.Request());
        await world.AcceptReviewedAsync(accepted.Batch!.Value);
        var elsewhere = await world.SubmitAsync(SubmissionWorld.Request() with { DomainId = SubmissionWorld.OtherDomain });

        Assert.Equal(
            (SubmissionState.Rejected, SubmissionState.Accepted, SubmissionState.Accepted),
            (refused.State, accepted.State, elsewhere.State));
        Assert.Null(refused.Reason!.PreviousBatch);
        Assert.NotEqual(ReasonCodes.AlreadyReceived, refused.Reason.Code);
    }

    [DatabaseFact]
    public async Task ItemSaysWhichBatchKeptItWhenItCameAndWhoSentIt()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        await world.AcceptReviewedAsync(outcome.Batch!.Value);
        var batch = await world.Service.DetailAsync(Granted, outcome.Batch.Value, TestContext.Current.CancellationToken);
        var item = long.Parse(Assert.Single(await world.StoredAsync("SELECT id::text FROM content.item_text ORDER BY id LIMIT 1")), CultureInfo.InvariantCulture);

        var provenance = await new BatchRegister(world.DataSource).ProvenanceAsync(Granted, item, TestContext.Current.CancellationToken);
        var hidden = await new BatchRegister(world.DataSource).ProvenanceAsync([SubmissionWorld.OtherDomain], item, TestContext.Current.CancellationToken);

        Assert.Equal(new ItemProvenance(outcome.Batch.Value, batch!.SubmittedAt, SubmissionWorld.Person, outcome.Submission), provenance);
        Assert.Null(hidden);
    }

    [DatabaseFact]
    public async Task RefusalAppearsInTheRegisterWithTheSameCodeTheSubmissionAnswered()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(content: "isto nao e um arquivo de conversa"));
        var listed = await world.Service.ListAsync(new BatchFilter { Domains = Granted, State = BatchState.Rejected }, TestContext.Current.CancellationToken);

        var entry = Assert.Single(listed);
        Assert.Equal((outcome.Batch, outcome.Reason!.Code, SubmissionWorld.Person), (entry.Id, entry.Reason, entry.SubmittedBy));
    }
}
