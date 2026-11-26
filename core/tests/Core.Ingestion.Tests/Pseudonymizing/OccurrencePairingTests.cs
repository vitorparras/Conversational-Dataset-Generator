using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// The group of an occurrence stored before the coreference (issue 102, F3): it takes the group of the occurrence the answer gives in the same text, at the same positions and of the
/// same kind, and keeps the destination it had; one with no such pair leaves the item with no group at all.
/// </summary>
public sealed class OccurrencePairingTests
{
    private static readonly DecidedOccurrence[] Stored = [Held(0, 4, "PERSON", Destination.Pseudonymize), Held(9, 13, "LOC", Destination.Remove)];

    [Fact]
    public void StoredOccurrenceTakesTheGroupOfItsPairInItsOwnText()
    {
        GroupedOccurrence[] answered = [Found(0, 0, 4, "PERSON", 7), Found(1, 9, 13, "LOC", 3), Found(1, 0, 4, "PERSON", 2), Found(1, 20, 24, "PERSON", 5)];

        var paired = OccurrencePairing.Paired(Stored, answered, 1);

        Assert.Equal(["0-4:PERSON:Pseudonymize:2", "9-13:LOC:Remove:3"], paired!.Select(Shown));
    }

    [Theory]
    [InlineData(0, 9, 13, "LOC")]
    [InlineData(1, 9, 14, "LOC")]
    [InlineData(1, 8, 13, "LOC")]
    [InlineData(1, 9, 13, "ORG")]
    public void StoredOccurrenceWithoutPairLeavesTheItemWithNoGroup(int text, int start, int end, string kind)
    {
        GroupedOccurrence[] answered = [Found(1, 0, 4, "PERSON", 2), Found(text, start, end, kind, 3)];

        Assert.Null(OccurrencePairing.Paired(Stored, answered, 1));
    }

    [Fact]
    public void ItemWithNoStoredOccurrenceIsPairedEmpty() => Assert.Empty(OccurrencePairing.Paired([], [Found(0, 0, 4, "PERSON", 1)], 0)!);

    [Fact]
    public void PairingWithoutTheStoredOccurrencesIsRefused() =>
        Assert.Equal("stored", Assert.Throws<ArgumentNullException>(() => OccurrencePairing.Paired(null!, [Found(0, 0, 4, "PERSON", 1)], 0)).ParamName);

    [Fact]
    public void PairingWithoutTheAnswerIsRefused() => Assert.Equal("answered", Assert.Throws<ArgumentNullException>(() => OccurrencePairing.Paired(Stored, null!, 0)).ParamName);

    private static DecidedOccurrence Held(int start, int end, string kind, Destination destination) => new(new Occurrence(start, end, kind, "", ""), destination);

    private static GroupedOccurrence Found(int text, int start, int end, string kind, int group) => new(text, start, end, kind, "PROPN", "nsubj", group);

    private static string Shown(DecidedOccurrence occurrence) =>
        $"{occurrence.Occurrence.Start}-{occurrence.Occurrence.End}:{occurrence.Occurrence.Kind}:{occurrence.Destination}:{occurrence.Group}";
}
