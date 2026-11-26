using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The swap of an item rerun (issue 102, F3): each mention of the new sanitized text is matched by its original to the marker it had in the map of the item, the author keeps the marker
/// of the author, a marker that stood for two originals stands for both, and an original the map does not hold gets a marker of its own, none the map holds nor one another batch holds.
/// </summary>
public sealed class RerunSwappingTests
{
    private const string Raw = "ZQX-FAKE-PERSON-1 liga (00) 90000-0000";
    private const string Original = "ZQX-FAKE-PERSON";
    private const string Author = "PESSOA_00000007";
    private const string Mentioned = "PESSOA_00000009";

    [Fact]
    public void KnownOriginalKeepsItsMarkerAndTheAuthorKeepsTheAuthorMarker()
    {
        var produced = Rerun(Entry(Author, "ZQXANA", true), Entry(Mentioned, Original));

        Assert.Equal($"{Mentioned}-1 liga [contato]", produced.Pseudonymized);
        Assert.Equal([$"{Author}:ZQXANA:True", $"{Mentioned}:{Original}:False"], Entries(produced));
    }

    [Fact]
    public void MarkerOfTwoOriginalsStandsForBoth()
    {
        var produced = Rerun(Entry(Mentioned, "ZQXZE"), Entry(Mentioned, Original));

        Assert.Equal($"{Mentioned}-1 liga [contato]", produced.Pseudonymized);
    }

    [Fact]
    public void OriginalTheMapDoesNotHoldGetsAMarkerOfItsOwn()
    {
        var produced = Rerun(Entry(Mentioned, "ZQXZE"));

        var marker = produced.Pseudonymized![..Mentioned.Length];
        Assert.Equal(($"{marker}-1 liga [contato]", true, false), (produced.Pseudonymized, marker.StartsWith("PESSOA_", StringComparison.Ordinal), marker == Mentioned));
        Assert.Contains($"{marker}:{Original}:False", Entries(produced));
    }

    /// <summary>The rerun of the item of <see cref="Raw"/>, stored with an older sanitized text, pseudonymized before with the map given.</summary>
    private static Resanitized Rerun(params NewMapEntry[] map)
    {
        var held = new RerunSource(Raw, [new ItemOccurrence(0, Original.Length, "PERSON", OccurrenceDestination.Pseudonymize)], "texto antigo zqx", null) { ItemId = 1, Map = map };
        var steps = new SanitizationRerun(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries()));
        return Assert.IsType<Resanitized>(DecidedItemTexts.Rerun(steps, CompositionRoot.SwapOf(), held));
    }

    private static NewMapEntry Entry(string marker, string original, bool author = false) => new(marker, original, author, OccurrenceDestination.Pseudonymize);

    private static string[] Entries(Resanitized produced) => [.. produced.Map!.Select(entry => $"{entry.Marker}:{entry.Original}:{entry.Author}").Order(StringComparer.Ordinal)];
}
