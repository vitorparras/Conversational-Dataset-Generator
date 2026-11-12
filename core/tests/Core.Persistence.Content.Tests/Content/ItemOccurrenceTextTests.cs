using Core.Persistence.Content;

namespace Core.Persistence.Tests.Content;

/// <summary>The text of an occurrence names the kind and the destination and never the offsets, which with the text rebuild what was found.</summary>
public sealed class ItemOccurrenceTextTests
{
    [Theory]
    [InlineData(OccurrenceDestination.Remove, "ItemOccurrence { Kind = email, Destination = Remove }")]
    [InlineData(OccurrenceDestination.Pseudonymize, "ItemOccurrence { Kind = email, Destination = Pseudonymize }")]
    [InlineData(OccurrenceDestination.Preserve, "ItemOccurrence { Kind = email, Destination = Preserve }")]
    public void TheTextNamesTheKindAndTheDestinationOfTheOccurrence(OccurrenceDestination destination, string expected)
    {
        var occurrence = new ItemOccurrence(31, 47, "email", destination);

        Assert.Equal(expected, occurrence.ToString());
    }

    [Fact]
    public void TheTextNeverHoldsTheOffsets()
    {
        var text = new ItemOccurrence(31415, 92653, "cpf", OccurrenceDestination.Remove).ToString();

        Assert.DoesNotContain("31415", text, StringComparison.Ordinal);
        Assert.DoesNotContain("92653", text, StringComparison.Ordinal);
    }
}
