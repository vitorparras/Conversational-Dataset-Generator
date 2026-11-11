using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Content.Writing;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// What the submission keeps of the loss of a material that does not fit the form exactly (issue 81): each item declares the form it received and what that form does not keep,
/// and one that fits declares nothing. The conference of the form runs before the storing: with the only reader there is (no passages, so no labeled sequence), a material the conference accepts
/// never loses anything, and what the form does not keep is proved at the storing step, with a material the conference would accept.
/// </summary>
public sealed class SubmissionAccommodationTests
{
    [DatabaseFact]
    public async Task ConversationDeclaredAsDocumentIsRefusedByTheConferenceBeforeAnythingCouldBeAccommodated()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request() with { DeclaredForm = MaterialForm.Document });

        Assert.Equal((SubmissionState.Rejected, 0L, 0L), (outcome.State, await world.ItemRowsAsync(), await world.AccommodatedRowsAsync()));
    }

    [DatabaseFact]
    public async Task StoringAMaterialTheConferenceAcceptedIntoAFormThatDoesNotKeepEverythingDeclaresTheFormAndTheLossOfEachItem()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var batches = new IngestionBatches(world.DataSource, OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]));
        var opened = await batches.OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person), TestContext.Current.CancellationToken);
        var instant = new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);
        var material = new ReadMaterial([new ReadUnit("ZQXTERCEIRO", "ZQXUM", instant, null, []), new ReadUnit("ZQXQUARTO", "ZQXDOIS", instant, null, [])]);
        var kept = new SubmissionStoring.Kept(SubmissionWorld.Domain, opened.BatchId!.Value, material, MaterialForm.Document);

        var stored = await new SubmissionStoring.Keeper(batches, new ContentWriter(world.DataSource, world.Content), NullLogger.Instance).KeepAsync(kept, TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionStoring.Keeping.Kept, 2L, 2L), (stored, await world.ItemRowsAsync(), await world.AccommodatedRowsAsync()));
    }

    [DatabaseFact]
    public async Task ConversationDeclaredAsConversationFitsExactlyAndNoItemDeclaresALoss()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());

        Assert.Equal((SubmissionState.Accepted, 2L, 0L), (outcome.State, await world.ItemRowsAsync(), await world.AccommodatedRowsAsync()));
    }
}
