using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;
using Core.Tests.Support.Harness;
using CsCheck;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>Each decided destination is applied to the text: removed, kept for the pseudonym, or kept as it came (issue 90).</summary>
public sealed class DestinationsStepTests
{
    private static readonly int[] Families = [0x4E00, 0x1F600, 0x0300];

    /// <summary>A text whose every code point is unique, with occurrences of any destination that may repeat, touch, cross or contain one another.</summary>
    private static readonly Gen<(string Raw, DecidedOccurrence[] Decided)> Decisions = Gen.Int[0, 2].Array[1, 12].SelectMany(families =>
    {
        var raw = string.Concat(families.Select((family, index) => char.ConvertFromUtf32(Families[family] + index)));
        var span = Gen.Int[0, families.Length].SelectMany(start => Gen.Int[start, families.Length].Select(end => (start, end)));
        var decided = Gen.Select(span, Gen.Int[0, 3], (cut, destination) => destination switch
        {
            0 => Credential(cut.start, cut.end),
            1 => Data(cut.start, cut.end),
            2 => Pseudonymized(cut.start, cut.end),
            _ => Preserved(cut.start, cut.end),
        });
        return decided.Array[0, 5].Select(occurrences => (raw, occurrences));
    });

    [Fact]
    [Trait("Kind", "Property")]
    public void NoCodePointOfAnOccurrenceToRemoveSurvivesAndNoneOutsideTheOccurrencesIsLost() =>
        PropertyRunner.FromProcess().Check(Decisions, generated =>
        {
            var (raw, decided) = generated;
            var length = Boundaries.CodePointCount(raw);
            var removed = Covered(decided.Where(occurrence => occurrence.Destination == Destination.Remove), length);
            var untouched = Covered(decided, length).Select(covered => !covered).ToArray();

            var text = Sanitized(raw, decided).Text;

            Assert.All(Enumerable.Range(0, length).Where(at => removed[at]), at => Assert.DoesNotContain(Cut(raw, at, at + 1), text, StringComparison.Ordinal));
            Assert.All(Enumerable.Range(0, length).Where(at => untouched[at]), at => Assert.Contains(Cut(raw, at, at + 1), text, StringComparison.Ordinal));
        });

    /// <summary>Which code points of the text fall inside any of the occurrences.</summary>
    private static bool[] Covered(IEnumerable<DecidedOccurrence> occurrences, int length)
    {
        var covered = new bool[length];
        foreach (var occurrence in occurrences)
        {
            covered.AsSpan(occurrence.Occurrence.Start, occurrence.Occurrence.End - occurrence.Occurrence.Start).Fill(true);
        }

        return covered;
    }

    [Fact]
    public void OccurrenceContainedInARemovedOneLeavesUnderItsMarker()
    {
        var inData = Sanitized("ab QXZ7KW cd", Data(3, 9), Data(5, 7));
        var pseudonymInCredential = Sanitized("ab QXZ7KW cd", Credential(3, 9), Pseudonymized(5, 7));

        Assert.Equal(("ab [dado] cd", "ab [credencial] cd"), (inData.Text, pseudonymInCredential.Text));
        Assert.Empty(pseudonymInCredential.Pseudonyms);
    }

    [Theory]
    [InlineData("ab Q cd", 3, 4)]
    [InlineData("ab QXZ7KWP9QXZ7KWP9 cd", 3, 19)]
    [InlineData("ab \U0001F600e\u0301 cd", 3, 6)]
    public void RemovedValueLeavesATypedMarkerWithNoCharacterOfIt(string raw, int start, int end)
    {
        var asData = Sanitized(raw, Data(start, end));
        var asCredential = Sanitized(raw, Credential(start, end));

        Assert.Equal(("ab [dado] cd", "ab [credencial] cd"), (asData.Text, asCredential.Text));
    }

    [Fact]
    public void PseudonymCarriedToTheSanitizedTextKeepsItsGroup()
    {
        var sanitized = Sanitized("chave QXZ7 de Wvkt", Credential(6, 10), Pseudonymized(14, 18) with { Group = 3 });

        var pseudonym = Assert.Single(sanitized.Pseudonyms);
        Assert.Equal((22, 26, 3), (pseudonym.Occurrence.Start, pseudonym.Occurrence.End, pseudonym.Group));
    }

    [Fact]
    public void PseudonymizedSpanStaysAndItsPositionResolves()
    {
        const string raw = "chave QXZ7 de Wvkt e Wvkt";

        var sanitized = Sanitized(raw, Credential(6, 10), Pseudonymized(14, 18));

        var pseudonym = Assert.Single(sanitized.Pseudonyms);
        Assert.Equal("chave [credencial] de Wvkt e Wvkt", sanitized.Text);
        Assert.Equal((22, 26, OccurrenceKinds.Person), (pseudonym.Occurrence.Start, pseudonym.Occurrence.End, pseudonym.Occurrence.Kind));
        Assert.Equal("Wvkt", Boundaries.Slice(sanitized.Text, pseudonym.Occurrence));
    }

    [Fact]
    public void CredentialIsRemovedAndNeverOfferedAsPseudonym()
    {
        const string raw = "token Kq7Zx9 do Wvkt";

        var sanitized = Sanitized(raw, Credential(6, 12), Pseudonymized(9, 12), Pseudonymized(16, 20));
        var secretName = Sanitized(raw, Decided("TOKEN", "PROPN", 16, 20));

        Assert.Equal("token [credencial] do Wvkt", sanitized.Text);
        Assert.Equal([(22, 26)], sanitized.Pseudonyms.Select(pseudonym => (pseudonym.Occurrence.Start, pseudonym.Occurrence.End)));
        Assert.Equal("token Kq7Zx9 do [credencial]", secretName.Text);
        Assert.Empty(secretName.Pseudonyms);
    }

    [Fact]
    public void OccurrencesThatOnlyTouchLeaveApartAndKeepEachItsDestination()
    {
        var beside = Sanitized("chave QXZ7Wvkt fim", Credential(6, 10), Pseudonymized(10, 14));
        var removedTwice = Sanitized("QXZ7Wvkt", Credential(0, 4), Data(4, 8));

        var pseudonym = Assert.Single(beside.Pseudonyms);
        Assert.Equal(("chave [credencial]Wvkt fim", "[credencial][dado]"), (beside.Text, removedTwice.Text));
        Assert.Equal("Wvkt", Boundaries.Slice(beside.Text, pseudonym.Occurrence));
    }

    [Fact]
    public void OverlappingOccurrencesBecomeOneMarkerThatKeepsTheCredential()
    {
        var withPseudonym = Sanitized("ab QXZ7KW cd", Credential(3, 7), Pseudonymized(5, 9));
        var withData = Sanitized("ab QXZ7KW cd", Credential(3, 7), Data(5, 9));

        Assert.Equal(("ab [credencial] cd", "ab [credencial] cd"), (withPseudonym.Text, withData.Text));
        Assert.Empty(withPseudonym.Pseudonyms);
    }

    [Fact]
    public void UnmarkedProperNameIsLeftAsWritten()
    {
        const string raw = "Zqvx e Wvkt, o Kvrtinho, e Zqvx foram";

        var sanitized = Sanitized(raw, Data(0, 4));

        Assert.Equal("[dado] e Wvkt, o Kvrtinho, e Zqvx foram", sanitized.Text);
        Assert.Empty(sanitized.Pseudonyms);
    }

    [Theory]
    [InlineData("pagou R$ 1.234,56 a QXZ7", 6, 17)]
    [InlineData("pagou 1.234 a QXZ7", 6, 11)]
    [InlineData("pagou em Kvrt a QXZ7", 9, 13)]
    public void PreservedSpanLeavesAsItCame(string raw, int start, int end)
    {
        var removed = Boundaries.CodePointCount(raw) - 4;

        var sanitized = Sanitized(raw, Preserved(start, end), Data(removed, removed + 4));

        Assert.Equal(Cut(raw, 0, removed) + "[dado]", sanitized.Text);
        Assert.Equal(Cut(raw, start, end), Cut(sanitized.Text, start, end));
        Assert.Empty(sanitized.Pseudonyms);
    }
}
