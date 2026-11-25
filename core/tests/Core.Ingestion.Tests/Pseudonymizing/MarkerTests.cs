using Core.Boundary.Contracts;
using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>The marker of a referent: drawn, never derived from the original, typed by the kind of the mention, and never one that is already in use.</summary>
public sealed class MarkerTests
{
    [Fact]
    public void MarkerNamesTheKindOfTheMention()
    {
        var conversation = new ConversationMarkers(new MarkerDraw(), _ => false);

        var person = conversation.Of(Referent.Group(1), MarkerKinds.Of(OccurrenceKinds.Person)!.Value);
        var place = conversation.Of(Referent.Group(2), MarkerKinds.Of(OccurrenceKinds.Location)!.Value);
        var organization = conversation.Of(Referent.Group(3), MarkerKinds.Of(OccurrenceKinds.Organization)!.Value);

        Assert.Matches("^PESSOA_[0-9]{8}$", person.Text);
        Assert.Matches("^LUGAR_[0-9]{8}$", place.Text);
        Assert.Matches("^ORGANIZACAO_[0-9]{8}$", organization.Text);
    }

    [Theory]
    [InlineData("TOKEN")]
    [InlineData("CPF")]
    [InlineData("person")]
    public void AKindThatIsNotANameHasNoMarker(string kind) => Assert.Null(MarkerKinds.Of(kind));

    [Fact]
    public void RedrawnMarkerNeverRepeatsOneInUse()
    {
        var inUse = new HashSet<Marker> { new(MarkerKind.Person, 7) };
        var conversation = Draws.Conversation(Draws.Of(7, 7, 12, 12, 7, 30), inUse);

        var first = conversation.Of(Referent.Author("ZQX-AUTOR-UM"), MarkerKind.Person);
        var second = conversation.Of(Referent.Author("ZQX-AUTOR-DOIS"), MarkerKind.Person);

        Assert.Equal((new Marker(MarkerKind.Person, 12), new Marker(MarkerKind.Person, 30)), (first, second));
    }

    [Fact]
    public void SameNumberOfAnotherKindIsAnotherMarker()
    {
        var inUse = new HashSet<Marker> { new(MarkerKind.Person, 7) };
        var conversation = Draws.Conversation(Draws.Of(7), inUse);

        var place = conversation.Of(Referent.Group(1), MarkerKind.Place);

        Assert.Equal("LUGAR_00000007", place.Text);
    }

    [Fact]
    public void SameNameInTwoDomainsGetsUnrelatedMarkers()
    {
        const string name = "ZQX-NOME-COMUM";
        var draw = new MarkerDraw();

        var markers = Enumerable.Range(0, 32).Select(_ => new ConversationMarkers(draw, _ => false).Of(Referent.Author(name), MarkerKind.Person)).ToList();

        Assert.Equal(markers.Count, markers.Distinct().Count());
    }

    [Fact]
    public void ReferentToldAsTextCarriesNoName()
    {
        var referent = Referent.Author("ZQX-AUTOR-SECRETO");

        Assert.DoesNotContain("ZQX-AUTOR-SECRETO", referent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void KnownReferentKeepsTheMarkerItsItemAlreadyHas()
    {
        var known = new Dictionary<Referent, Marker> { [Referent.Author("ZQX-AUTOR-UM")] = new(MarkerKind.Person, 4_829_130) };
        var conversation = new ConversationMarkers(Draws.Of(4_829_130, 5), _ => false, known);

        var again = conversation.Of(Referent.Author("ZQX-AUTOR-UM"), MarkerKind.Person);
        var other = conversation.Of(Referent.Author("ZQX-AUTOR-DOIS"), MarkerKind.Person);

        Assert.Equal(("PESSOA_04829130", "PESSOA_00000005"), (again.Text, other.Text));
    }
}
