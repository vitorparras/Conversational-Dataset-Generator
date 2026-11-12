using Core.Persistence.Content;

namespace Core.Persistence.Tests.Content;

/// <summary>An occurrence that starts before the text, ends before it starts, has no kind or names a destination that is not one of the three is refused when it is built.</summary>
public sealed class ItemOccurrenceGuardTests
{
    [Fact]
    public void AnOccurrenceThatStartsBeforeTheTextIsRefused()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new ItemOccurrence(-1, 4, "email", OccurrenceDestination.Remove));

        Assert.Equal("start", refused.ParamName);
    }

    [Fact]
    public void AnOccurrenceThatEndsBeforeItStartsIsRefused()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new ItemOccurrence(5, 4, "email", OccurrenceDestination.Remove));

        Assert.Equal("end", refused.ParamName);
    }

    [Fact]
    public void AnOccurrenceWithoutAKindIsRefused()
    {
        var refused = Assert.Throws<ArgumentNullException>(() => new ItemOccurrence(0, 4, null!, OccurrenceDestination.Remove));

        Assert.Equal("kind", refused.ParamName);
    }

    [Fact]
    public void AnEmptyOccurrenceIsAccepted()
    {
        var occurrence = new ItemOccurrence(4, 4, "email", OccurrenceDestination.Preserve);

        Assert.Equal(4, occurrence.Start);
        Assert.Equal(4, occurrence.End);
    }

    [Fact]
    public void ADestinationThatIsNotOneOfTheThreeIsRefusedWithTheReason()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new ItemOccurrence(0, 4, "email", (OccurrenceDestination)99));

        Assert.Equal("destination", refused.ParamName);
        Assert.Contains("The destination is not one of the three.", refused.Message, StringComparison.Ordinal);
        Assert.Equal((OccurrenceDestination)99, refused.ActualValue);
    }
}
