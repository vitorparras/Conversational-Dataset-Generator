using Core.Persistence.Content;

namespace Core.Persistence.Tests.Content;

/// <summary>What was sealed is read back exactly as it was written, and what is not a list of occurrences, or names a destination that is not one of the three, is refused with the reason.</summary>
public sealed class OccurrenceJsonTests
{
    [Fact]
    public void WhatIsWrittenIsReadBackWithEveryField()
    {
        ItemOccurrence[] written = [new(3, 9, "email", OccurrenceDestination.Pseudonymize), new(12, 20, "cpf", OccurrenceDestination.Preserve)];

        var read = OccurrenceJson.Read(OccurrenceJson.Write(written));

        Assert.Equal(written, read);
    }

    [Fact]
    public void ADestinationWrittenInAnotherCaseIsNotOneOfTheThreeAndIsRefusedWithTheReason()
    {
        var json = OccurrenceJson.Write([new(0, 5, "email", OccurrenceDestination.Remove)]).Replace("\"Remove\"", "\"remove\"", StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => OccurrenceJson.Read(json));

        Assert.Equal("The sealed occurrences name a destination that is not one of the three.", refused.Message);
    }

    [Fact]
    public void ADestinationThatIsNotNamedIsRefusedWithTheReason()
    {
        var json = OccurrenceJson.Write([new(0, 5, "email", OccurrenceDestination.Remove)]).Replace("\"Remove\"", "\"Burn\"", StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => OccurrenceJson.Read(json));

        Assert.Equal("The sealed occurrences name a destination that is not one of the three.", refused.Message);
    }

    [Fact]
    public void EveryOccurrenceIsWrittenWithItsLimitsInCodePoints()
    {
        var json = OccurrenceJson.Write([new(0, 5, "email", OccurrenceDestination.Remove)]);

        Assert.Equal(/*lang=json,strict*/ """[{"Start":0,"End":5,"Kind":"email","Destination":"Remove","Unit":"code-point"}]""", json);
    }

    [Fact]
    public void OccurrencesCountedInAnotherUnitAreRefusedWithTheReason()
    {
        var json = OccurrenceJson.Write([new(0, 5, "email", OccurrenceDestination.Remove)]).Replace("\"code-point\"", "\"utf-16\"", StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => OccurrenceJson.Read(json));

        Assert.Equal("The sealed occurrences count their limits in another unit.", refused.Message);
    }

    [Fact]
    public void OccurrencesSealedBeforeTheUnitWasWrittenReadInCodePoints()
    {
        var read = OccurrenceJson.Read(/*lang=json,strict*/ """[{"Start":2,"End":7,"Kind":"email","Destination":"Remove"}]""");

        Assert.Equal([new ItemOccurrence(2, 7, "email", OccurrenceDestination.Remove)], read);
    }

    [Fact]
    public void WhatIsNotAListOfOccurrencesIsRefusedWithTheReason()
    {
        var refused = Assert.Throws<InvalidDataException>(() => OccurrenceJson.Read("null"));

        Assert.Equal("The sealed occurrences are not a list.", refused.Message);
    }
}
