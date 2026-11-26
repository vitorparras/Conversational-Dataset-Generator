using Core.Boundary.Contracts;
using Core.Host.Submissions;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The markers of a conversation are reserved for its batch before any of its items is written (0057): a marker another batch holds is drawn again without it, the conversation gives
/// up after the draws it has, and the items of a material that is no conversation are each a conversation of their own, sharing no marker.
/// </summary>
public sealed class SubmissionDrawsTests
{
    private static readonly SwapRequest[] TwoMessages = [Mentioning(Referent.Group(1)), Mentioning(Referent.Group(1))];

    [Fact]
    public async Task MarkerTakenByAnotherBatchIsDrawnAgainWithoutIt()
    {
        var asked = new List<string[]>();

        var swapped = await ConversationDraws.SwappedAsync(CompositionRoot.SwapOf(), (TwoMessages, true), markers =>
        {
            asked.Add(markers);
            return Task.FromResult<IReadOnlyList<string>>(asked.Count == 1 ? [markers[0]] : []);
        });

        Assert.Equal(2, asked.Count);
        Assert.DoesNotContain(asked[0][0], asked[1]);
        Assert.Equal(asked[1], MarkersOf(swapped).Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task MarkersTakenInEveryDrawFailTheWrite()
    {
        var asked = 0;

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => ConversationDraws.SwappedAsync(CompositionRoot.SwapOf(), (TwoMessages, true), markers =>
        {
            asked++;
            return Task.FromResult<IReadOnlyList<string>>(markers);
        }));

        Assert.Equal(ConversationDraws.Draws, asked);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task OnlyTheMessagesOfAConversationShareTheMarkerOfAGroup(bool conversation, int markers)
    {
        var swapped = await ConversationDraws.SwappedAsync(CompositionRoot.SwapOf(), (TwoMessages, conversation), _ => Task.FromResult<IReadOnlyList<string>>([]));

        Assert.Equal(markers, MarkersOf(swapped).Distinct(StringComparer.Ordinal).Count());
    }

    private static IEnumerable<string> MarkersOf(IEnumerable<Pseudonymized> swapped) => swapped.SelectMany(item => item.Entries).Select(entry => entry.Marker.Text);

    private static SwapRequest Mentioning(Referent referent) => new()
    {
        Sanitized = "ZQXA",
        Occurrences = [new ReferredOccurrence(new DecidedOccurrence(new Occurrence(0, 4, OccurrenceKinds.Person, "PROPN", "nsubj"), Destination.Pseudonymize), referent)],
    };
}
