using Core.Boundary.Contracts;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>Each decided destination is applied to the text: removed, kept for the pseudonym, or kept as it came (issue 90).</summary>
public sealed class DestinationsStepTests
{
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
    public void PseudonymizedSpanStaysAndItsPositionResolves()
    {
        const string raw = "chave QXZ7 de Wvkt e Wvkt";

        var sanitized = Sanitized(raw, Credential(6, 10), Pseudonymized(14, 18));

        var pseudonym = Assert.Single(sanitized.Pseudonyms);
        Assert.Equal("chave [credencial] de Wvkt e Wvkt", sanitized.Text);
        Assert.Equal((22, 26, OccurrenceKinds.Person), (pseudonym.Start, pseudonym.End, pseudonym.Kind));
        Assert.Equal("Wvkt", Boundaries.Slice(sanitized.Text, pseudonym));
    }

    [Fact]
    public void CredentialIsRemovedAndNeverOfferedAsPseudonym()
    {
        const string raw = "token Kq7Zx9 do Wvkt";

        var sanitized = Sanitized(raw, Credential(6, 12), Pseudonymized(9, 12), Pseudonymized(16, 20));

        Assert.Equal("token [credencial] do Wvkt", sanitized.Text);
        Assert.Equal([(22, 26)], sanitized.Pseudonyms.Select(pseudonym => (pseudonym.Start, pseudonym.End)));
        Assert.DoesNotContain(sanitized.Pseudonyms, pseudonym => OccurrenceKinds.IsSecret(pseudonym.Kind));
    }

    [Fact]
    public void OccurrencesThatOnlyTouchLeaveApartAndKeepEachItsDestination()
    {
        var beside = Sanitized("chave QXZ7Wvkt fim", Credential(6, 10), Pseudonymized(10, 14));
        var removedTwice = Sanitized("QXZ7Wvkt", Credential(0, 4), Data(4, 8));

        var pseudonym = Assert.Single(beside.Pseudonyms);
        Assert.Equal(("chave [credencial]Wvkt fim", "[credencial][dado]"), (beside.Text, removedTwice.Text));
        Assert.Equal("Wvkt", Boundaries.Slice(beside.Text, pseudonym));
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
        const string raw = "Zqvx e Wvkt, o Kvrtinho, foram";

        var sanitized = Sanitized(raw, Data(0, 4));

        Assert.Equal("[dado] e Wvkt, o Kvrtinho, foram", sanitized.Text);
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
