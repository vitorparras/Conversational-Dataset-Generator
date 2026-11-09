using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// What the submission keeps of the loss of a material that does not fit the form exactly (issue 81): each item declares the form it received and what that form does not keep, and one that fits declares nothing.
/// </summary>
public sealed class SubmissionAccommodationTests
{
    [DatabaseFact]
    public async Task ConversationDeclaredAsDocumentKeepsEachItemWithTheFormAndTheAuthorAndTheInstantItLost()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request() with { DeclaredForm = MaterialForm.Document });

        Assert.Equal((SubmissionState.Accepted, 2L, 2L), (outcome.State, await world.ItemRowsAsync(), await world.AccommodatedRowsAsync()));
    }

    [DatabaseFact]
    public async Task ConversationDeclaredAsConversationFitsExactlyAndNoItemDeclaresALoss()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());

        Assert.Equal((SubmissionState.Accepted, 2L, 0L), (outcome.State, await world.ItemRowsAsync(), await world.AccommodatedRowsAsync()));
    }
}
