namespace Core.Ingestion.Tests;

/// <summary>
/// The criterion of the replicated content (issue 82, criteria 1 to 3): a message is marked when the text is equal, long enough and from more than one sender, the three together; each alone marks
/// nothing, and the mark points to the first occurrence, which is marked too.
/// </summary>
public sealed class ReplicationTests
{
    private static readonly ReplicationRule Rule = new(minLength: 10, minSenders: 2);

    private static readonly DateTimeOffset Start = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheSameLongTextFromTwoSendersMarksBothAndBothPointToTheFirst()
    {
        var first = Said(1, 0, "ZQX-texto", "ZQX-ana", 40, minute: 0);
        var second = Said(2, 0, "ZQX-texto", "ZQX-bia", 40, minute: 5);

        var marks = Replication.Marks(Rule, [], [second, first]);

        Assert.Equal(new[] { Mark(1, 0, 1, 0), Mark(2, 0, 1, 0) }, marks);
    }

    [Fact]
    public void TheSameTextFromTheSameSenderIsNotMarkedHoweverManyTimesItIsRepeated()
    {
        var repeated = Enumerable.Range(0, 6).Select(at => Said(1, at, "ZQX-texto", "ZQX-ana", 40, minute: at)).ToList();

        Assert.Empty(Replication.Marks(Rule, [], repeated));
    }

    [Fact]
    public void ATextBelowTheMinimumLengthIsNotMarkedHoweverManySendersSayIt()
    {
        var greetings = Enumerable.Range(0, 5).Select(at => Said(1, at, "ZQX-oi", "ZQX-remetente-" + at, length: 9, minute: at)).ToList();

        Assert.Empty(Replication.Marks(Rule, [], greetings));
    }

    [Fact]
    public void ATextAtExactlyTheMinimumLengthIsMarked()
    {
        var marks = Replication.Marks(Rule, [], [Said(1, 0, "ZQX-texto", "ZQX-ana", 10, minute: 0), Said(1, 1, "ZQX-texto", "ZQX-bia", 10, minute: 1)]);

        Assert.Equal(2, marks.Count);
    }

    [Fact]
    public void TextsThatDifferAreNotMarkedEvenFromDifferentSenders()
    {
        var marks = Replication.Marks(Rule, [], [Said(1, 0, "ZQX-um", "ZQX-ana", 40, minute: 0), Said(1, 1, "ZQX-outro", "ZQX-bia", 40, minute: 1)]);

        Assert.Empty(marks);
    }

    [Fact]
    public void ARepeatedTextThatLaterComesFromAnotherSenderMarksTheOnesAlreadyKeptToo()
    {
        var kept = new List<ReplicationOccurrence> { Said(1, 0, "ZQX-texto", "ZQX-ana", 40, minute: 0), Said(1, 1, "ZQX-texto", "ZQX-ana", 40, minute: 1) };
        var arriving = new List<ReplicationOccurrence> { Said(2, 0, "ZQX-texto", "ZQX-bia", 40, minute: 9) };

        var marks = Replication.Marks(Rule, kept, arriving);

        Assert.Equal(new[] { Mark(1, 0, 1, 0), Mark(1, 1, 1, 0), Mark(2, 0, 1, 0) }, marks);
    }

    [Fact]
    public void AMessageThatArrivesAfterTheFirstOccurrenceOfAnotherBatchPointsToThatFirstOne()
    {
        var kept = new List<ReplicationOccurrence> { Said(1, 3, "ZQX-texto", "ZQX-ana", 40, minute: 0) };
        var arriving = new List<ReplicationOccurrence> { Said(7, 0, "ZQX-texto", "ZQX-bia", 40, minute: 0) };

        Assert.Equal(new[] { Mark(1, 3, 1, 3), Mark(7, 0, 1, 3) }, Replication.Marks(Rule, kept, arriving));
    }

    [Fact]
    public void NothingIsMarkedWhenNothingArrivesEvenIfTheKeptOnesWouldBe()
    {
        var kept = new List<ReplicationOccurrence> { Said(1, 0, "ZQX-texto", "ZQX-ana", 40, minute: 0), Said(1, 1, "ZQX-texto", "ZQX-bia", 40, minute: 1) };

        Assert.Empty(Replication.Marks(Rule, kept, []));
    }

    [Fact]
    public void ThreeSendersNeededRefusesTwoAndMarksThree()
    {
        var three = new ReplicationRule(minLength: 10, minSenders: 3);
        var two = new List<ReplicationOccurrence> { Said(1, 0, "ZQX-texto", "ZQX-ana", 40, minute: 0), Said(1, 1, "ZQX-texto", "ZQX-bia", 40, minute: 1) };
        var all = two.Append(Said(1, 2, "ZQX-texto", "ZQX-caio", 40, minute: 2)).ToList();

        Assert.Empty(Replication.Marks(three, [], two));
        Assert.Equal(3, Replication.Marks(three, [], all).Count);
    }

    [Fact]
    public void TheMarkIsMadeOfPlacesAndDigestsOnlyNeverOfTheTextOrOfTheSender()
    {
        var properties = typeof(ReplicationMark).GetProperties().Where(property => property.GetIndexParameters().Length == 0).ToList();

        Assert.Equal(new[] { "First", "Of" }, properties.Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.All(properties, property => Assert.Equal(typeof(ReplicationPlace), property.PropertyType));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    [InlineData(10, 1)]
    [InlineData(10, 0)]
    public void ACriterionBelowWhatMakesSenseIsRefused(int length, int senders) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ReplicationRule(length, senders));

    private static ReplicationOccurrence Said(long item, int position, string text, string sender, int length, int minute) =>
        new(new ReplicationPlace(item, position), text, sender, length, Start.AddMinutes(minute));

    private static ReplicationMark Mark(long item, int position, long firstItem, int firstPosition) =>
        new(new ReplicationPlace(item, position), new ReplicationPlace(firstItem, firstPosition));
}
