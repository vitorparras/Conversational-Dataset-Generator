using Core.Boundary.Contracts;
using Core.Boundary.Text;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// The swap writes the marker of each referent in place of its span of the sanitized text and gives the map of the item, marker and original; only an occurrence to pseudonymize
/// becomes an entry (issue 101, CA-5), and only the map of the item itself is taken (decision 0057).
/// </summary>
public sealed class PseudonymSwapTests
{
    private const string Text = "oi ZQXA e ZQXB, ZQXA";

    private static readonly TextBoundaries Boundaries = new();

    /// <summary>The swap with the opening of the conversation the composition root gives it, over the draw of the test.</summary>
    private static PseudonymSwap SwapOf(MarkerDraw draw) => new(Boundaries, (inUseElsewhere, known) => new ConversationMarkers(draw, inUseElsewhere, known));

    [Fact]
    public void SwapWritesTheMarkerOfEachReferentInPlaceOfItsSpanAndTheMapHoldsTheOriginal()
    {
        var swap = SwapOf(Draws.Of(11, 22));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1)), Person(10, 14, Referent.Group(2)), Person(16, 20, Referent.Group(1))] };

        var swapped = swap.Swap(request, _ => false);

        Assert.Equal("oi PESSOA_00000011 e PESSOA_00000022, PESSOA_00000011", swapped.Text);
        Assert.Equal([("PESSOA_00000011", "ZQXA"), ("PESSOA_00000022", "ZQXB")], swapped.Entries.Select(entry => (entry.Marker.Text, entry.Original)));
    }

    [Fact]
    public void RemovedSpanNeverBecomesAMapEntry()
    {
        var swap = SwapOf(Draws.Of());
        var removed = new ReferredOccurrence(new DecidedOccurrence(new Occurrence(3, 7, OccurrenceKinds.Person, "PROPN", "obj"), Destination.Remove), Referent.Group(1));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(10, 14, Referent.Group(2)), removed] };

        _ = Assert.Throws<PseudonymOfRemovedException>(() => swap.Swap(request, _ => false));
    }

    [Fact]
    public void PreservedSpanNeverBecomesAMapEntryEither()
    {
        var swap = SwapOf(Draws.Of());
        var preserved = new ReferredOccurrence(new DecidedOccurrence(new Occurrence(3, 7, OccurrenceKinds.Location, "PROPN", "obj"), Destination.Preserve), Referent.Group(1));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [preserved] };

        _ = Assert.Throws<PseudonymOfRemovedException>(() => swap.Swap(request, _ => false));
    }

    [Fact]
    public void SwapTakesNoMapButTheOneOfItsItem()
    {
        var swap = SwapOf(Draws.Of());
        var ofAnother = new ItemMap(8, [new MapEntry(Referent.Group(1), new Marker(MarkerKind.Person, 5), "ZQXA")]);
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1))], ItemId = 7, Map = ofAnother };

        var refused = Assert.Throws<ArgumentException>(() => swap.Swap(request, _ => false));

        Assert.Equal("request", refused.ParamName);
    }

    [Fact]
    public void MapOfTheItemGivesTheMarkersItAlreadyHasWithoutDrawing()
    {
        var swap = SwapOf(Draws.Of(99));
        var map = new ItemMap(7, [new MapEntry(Referent.Group(1), new Marker(MarkerKind.Person, 5), "ZQXA")]);
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1))], ItemId = 7, Map = map };

        var swapped = swap.Swap(request, _ => false);

        Assert.Equal("oi PESSOA_00000005 e ZQXB, ZQXA", swapped.Text);
    }

    [Fact]
    public void OverlappingOccurrencesBecomeOneMarkerOverTheirUnion()
    {
        var swap = SwapOf(Draws.Of(1, 2));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(5, 9, Referent.Group(2)), Person(3, 7, Referent.Group(1))] };

        var swapped = swap.Swap(request, _ => false);

        Assert.Equal("oi PESSOA_00000001 ZQXB, ZQXA", swapped.Text);
        Assert.Equal([("PESSOA_00000001", "ZQXA e")], swapped.Entries.Select(entry => (entry.Marker.Text, entry.Original)));
    }

    [Fact]
    public void AdjacentOccurrencesAreBothSwapped()
    {
        var swap = SwapOf(Draws.Of(1, 2));
        var request = new SwapRequest { Sanitized = "ZQXAZQXB", Occurrences = [Person(4, 8, Referent.Group(2)), Person(0, 4, Referent.Group(1))] };

        var swapped = swap.Swap(request, _ => false);

        Assert.Equal("PESSOA_00000001PESSOA_00000002", swapped.Text);
    }

    [Fact]
    public void SpanIsCutByCodePointsPastEmoji()
    {
        var swap = SwapOf(Draws.Of(3));
        var request = new SwapRequest { Sanitized = "😀 ZQXA fim", Occurrences = [Person(2, 6, Referent.Author("ZQXA"))] };

        var swapped = swap.Swap(request, _ => false);

        Assert.Equal(("😀 PESSOA_00000003 fim", "ZQXA", true), (swapped.Text, swapped.Entries.Single().Original, swapped.Entries.Single().IsAuthor));
    }

    [Fact]
    public void MarkerInUseElsewhereIsDrawnAgain()
    {
        var swap = SwapOf(Draws.Of(4, 6));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1))] };

        var swapped = swap.Swap(request, marker => marker.Number == 4);

        Assert.Equal(6, swapped.Entries.Single().Marker.Number);
    }

    private static ReferredOccurrence Person(int start, int end, Referent referent) =>
        new(new DecidedOccurrence(new Occurrence(start, end, OccurrenceKinds.Person, "PROPN", "obj"), Destination.Pseudonymize), referent);
}
