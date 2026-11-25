using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// What the markers refuse: an argument that is missing, a kind that is not one of the three, a number outside the eight digits; and the identity of a referent, which is its
/// source and its identity within it, told without the name.
/// </summary>
public sealed class MarkerArgumentsTests
{
    private const string UnknownKind = "The kind of marker is not known.";

    [Fact]
    public void ConversationWithoutItsDrawIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new ConversationMarkers(null!, _ => false, new Dictionary<Referent, Marker>()));

        Assert.Equal("draw", defect.ParamName);
    }

    [Fact]
    public void ConversationWithoutTheMarkersInUseElsewhereIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new ConversationMarkers(Draws.Of(), null!, new Dictionary<Referent, Marker>()));

        Assert.Equal("inUseElsewhere", defect.ParamName);
    }

    [Fact]
    public void ConversationWithoutTheMarkersItKnowsIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new ConversationMarkers(Draws.Of(), _ => false, null!));

        Assert.Equal("known", defect.ParamName);
    }

    [Fact]
    public void MarkerOfNoReferentIsADefect()
    {
        var conversation = new ConversationMarkers(Draws.Of(), _ => false);

        var defect = Assert.Throws<ArgumentNullException>(() => conversation.Of(null!, MarkerKind.Person));

        Assert.Equal("referent", defect.ParamName);
    }

    [Fact]
    public void MarkerOfAKindThatIsNotOneOfTheThreeIsRefusedWithTheReason()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new Marker((MarkerKind)99, 0));

        Assert.Equal("kind", refused.ParamName);
        Assert.StartsWith(UnknownKind, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000_000)]
    public void MarkerNumberOutsideTheEightDigitsIsRefused(int number)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new Marker(MarkerKind.Person, number));

        Assert.Equal("number", refused.ParamName);
    }

    [Fact]
    public void LargestNumberOfEightDigitsIsAMarker() => Assert.Equal("PESSOA_99999999", new Marker(MarkerKind.Person, Marker.Numbers - 1).Text);

    [Fact]
    public void DrawWithoutItsNumbersIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new MarkerDraw(null!));

        Assert.Equal("next", defect.ParamName);
    }

    [Fact]
    public void DrawThatCannotTellAMarkerInUseIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => Draws.Of(5).Draw(MarkerKind.Person, null!));

        Assert.Equal("inUse", defect.ParamName);
    }

    [Fact]
    public void KindOfMarkerOfNoKindOfOccurrenceIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => MarkerKinds.Of(null!));

        Assert.Equal("occurrenceKind", defect.ParamName);
    }

    [Fact]
    public void PrefixOfAKindThatIsNotOneOfTheThreeIsRefusedWithTheReason()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => MarkerKinds.Prefix((MarkerKind)99));

        Assert.Equal("kind", refused.ParamName);
        Assert.StartsWith(UnknownKind, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorWithoutANameIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => Referent.Author(null!));

        Assert.Equal("name", defect.ParamName);
    }

    [Fact]
    public void ReferentIsTheSameOnlyWithTheSameSourceAndTheSameIdentity()
    {
        var author = Referent.Author("1");

        Assert.Equal(Referent.Author("1"), author);
        Assert.NotEqual(Referent.Group(1), author);
        Assert.NotEqual(Referent.Author("2"), author);
        Assert.False(author.Equals(null));
    }

    [Fact]
    public void ReferentToldAsTextSaysOnlyWhereItComesFrom() =>
        Assert.Equal(("Referent { Source = Author }", "Referent { Source = Group }"), (Referent.Author("ZQX-AUTOR").ToString(), Referent.Group(3).ToString()));
}
