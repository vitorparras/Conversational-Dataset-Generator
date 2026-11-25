using Core.Boundary.Contracts;
using Core.Boundary.Text;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// The swap refuses, by name and with its own refusal, what it cannot pseudonymize: a missing argument, an occurrence outside the text or one no marker stands for; where
/// occurrences overlap, the widest names the union; and no part of the pseudonymization says the original when it is written out (issue 101, CA-5).
/// </summary>
public sealed class PseudonymSwapArgumentsTests
{
    private const string Text = "oi ZQXA e ZQXB, ZQXA";

    private static readonly TextBoundaries Boundaries = new();

    [Fact]
    public void MissingRequestOrCheckIsRefusedByItsName()
    {
        // The opening ignores the check it gets, so that only the swap itself can refuse a missing one.
        var swap = new PseudonymSwap(Boundaries, (_, known) => new ConversationMarkers(Draws.Of(1), _ => false, known));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1))] };

        var withoutRequest = Assert.Throws<ArgumentNullException>(() => swap.Swap(null!, _ => false));
        var withoutCheck = Assert.Throws<ArgumentNullException>(() => swap.Swap(request, null!));

        Assert.Equal(("request", "inUseElsewhere"), (withoutRequest.ParamName, withoutCheck.ParamName));
    }

    [Fact]
    public void MapOfAnotherItemIsRefusedWithItsOwnRefusal()
    {
        var ofAnother = new ItemMap(8, [new MapEntry(Referent.Group(1), new Marker(MarkerKind.Person, 5), "ZQXA")]);
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1))], ItemId = 7, Map = ofAnother };

        var refused = Assert.Throws<ArgumentException>(() => SwapOf(Draws.Of()).Swap(request, _ => false));

        Assert.StartsWith("The swap takes no map but the one of its item.", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(3, 3)]
    [InlineData(7, 3)]
    [InlineData(3, 21)]
    public void OccurrenceOutsideTheTextIsRefusedByName(int start, int end)
    {
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(start, end, Referent.Group(1))] };

        var refused = Assert.Throws<ArgumentException>(() => SwapOf(Draws.Of(1)).Swap(request, _ => false));

        Assert.Equal("occurrences", refused.ParamName);
        Assert.StartsWith("An occurrence falls outside the text.", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OccurrenceThatReachesTheLastCodePointIsSwapped()
    {
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(16, 20, Referent.Group(1))] };

        var swapped = SwapOf(Draws.Of(1)).Swap(request, _ => false);

        Assert.Equal("oi ZQXA e ZQXB, PESSOA_00000001", swapped.Text);
    }

    [Fact]
    public void OccurrenceNoMarkerStandsForIsRefusedByName()
    {
        var secret = new ReferredOccurrence(new DecidedOccurrence(new Occurrence(3, 7, "CREDENTIAL", "PROPN", "obj"), Destination.Pseudonymize), Referent.Group(1));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [secret] };

        var refused = Assert.Throws<ArgumentException>(() => SwapOf(Draws.Of(1)).Swap(request, _ => false));

        Assert.Equal("occurrences", refused.ParamName);
        Assert.StartsWith("An occurrence names nothing a marker stands for.", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OfOccurrencesThatStartTogetherTheWidestNamesTheUnion()
    {
        var place = new ReferredOccurrence(new DecidedOccurrence(new Occurrence(3, 9, OccurrenceKinds.Location, "PROPN", "obj"), Destination.Pseudonymize), Referent.Group(2));
        var request = new SwapRequest { Sanitized = Text, Occurrences = [Person(3, 7, Referent.Group(1)), place] };

        var swapped = SwapOf(Draws.Of(1, 2)).Swap(request, _ => false);

        Assert.Equal("oi LUGAR_00000001 ZQXB, ZQXA", swapped.Text);
        Assert.Equal([(Referent.Group(2), "ZQXA e")], swapped.Entries.Select(entry => (entry.Referent, entry.Original)));
    }

    [Fact]
    public void WrittenOutThePseudonymizationNamesItsPartsAndNeverTheOriginal()
    {
        var entry = new MapEntry(Referent.Group(1), new Marker(MarkerKind.Person, 5), "ZQXA");
        var occurrence = Person(3, 7, Referent.Group(1));
        string[] written =
        [
            new ItemMap(7, [entry]).ToString(),
            entry.ToString(),
            new Pseudonymized("oi PESSOA_00000005", [entry]).ToString(),
            occurrence.ToString(),
            new SwapRequest { Sanitized = Text, Occurrences = [occurrence], ItemId = 7 }.ToString(),
        ];

        Assert.Equal(
            [
                "ItemMap { ItemId = 7, Entries = 1 }",
                "MapEntry { Marker = PESSOA_00000005 }",
                "Pseudonymized { Entries = 1 }",
                "ReferredOccurrence { Decided = DecidedOccurrence { Kind = PERSON, Destination = Pseudonymize }, Referent = Referent { Source = Group } }",
                "SwapRequest { ItemId = 7, Occurrences = 1 }",
            ],
            written);
        Assert.DoesNotContain(written, text => text.Contains("ZQXA", StringComparison.Ordinal));
    }

    [Fact]
    public void PseudonymOfRemovedSaysTheDefectWithNoWordOfTheSpan() =>
        Assert.Equal("A span whose destination is not to pseudonymize never becomes an entry of the map.", new PseudonymOfRemovedException().Message);

    private static PseudonymSwap SwapOf(MarkerDraw draw) => new(Boundaries, (inUseElsewhere, known) => new ConversationMarkers(draw, inUseElsewhere, known));

    private static ReferredOccurrence Person(int start, int end, Referent referent) =>
        new(new DecidedOccurrence(new Occurrence(start, end, OccurrenceKinds.Person, "PROPN", "obj"), Destination.Pseudonymize), referent);
}
