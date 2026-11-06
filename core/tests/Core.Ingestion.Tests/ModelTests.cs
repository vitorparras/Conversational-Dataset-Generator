namespace Core.Ingestion.Tests;

/// <summary>What the reader hands over and what the conference gives: nothing in it is null, and what is shown of it never holds the text, the author or the author answered.</summary>
public sealed class ModelTests
{
    private const string Marker = "ZQXMARCADOR";
    private static readonly Passage[] OnePassage = [new Passage(0, 1)];

    [Fact]
    public void TheToStringOfAUnitTellsOnlyWhatItCarries()
    {
        var full = new ReadUnit(Marker, Marker, Units.First, Marker, OnePassage);
        var bare = new ReadUnit(Marker, null, null, null, []);

        Assert.Equal(
            ["ReadUnit { Author = True, Instant = True, RepliesTo = True, Passages = 1 }", "ReadUnit { Author = False, Instant = False, RepliesTo = False, Passages = 0 }"],
            [full.ToString(), bare.ToString()]);
    }

    [Fact]
    public void TheToStringOfTheFormsTellsOnlyHowManyPartsTheyHaveAndWhetherThereIsAMoment()
    {
        var message = new Message(Marker, Units.First, Marker, Marker);

        Assert.Equal(
            [
                "Message { Instant = 2031-03-04T08:00:00.0000000+00:00 }",
                "Conversation { Messages = 1 }",
                "Comment { Instant = True }",
                "Comment { Instant = False }",
                "Document { Sections = 2 }",
                "LabeledText { Passages = 1 }",
                "LabeledSequence { Texts = 1 }",
            ],
            [
                message.ToString(),
                new Conversation([message]).ToString(),
                new Comment(Marker, Units.First, Marker).ToString(),
                new Comment(Marker, null, Marker).ToString(),
                new Document([Marker, Marker]).ToString(),
                new LabeledText(Marker, OnePassage).ToString(),
                new LabeledSequence([new LabeledText(Marker, OnePassage)]).ToString(),
            ]);
    }

    [Fact]
    public void NothingTheFormsHoldIsNull()
    {
        var comment = Assert.Throws<ArgumentNullException>(() => new Comment(null!, null, "t"));
        var text = Assert.Throws<ArgumentNullException>(() => new Comment("a", null, null!));
        var conversation = Assert.Throws<ArgumentNullException>(() => new Conversation(null!));
        var document = Assert.Throws<ArgumentNullException>(() => new Document(null!));
        var sequence = Assert.Throws<ArgumentNullException>(() => new LabeledSequence(null!));

        Assert.Equal(["author", "text", "messages", "sections", "texts"], [comment.ParamName, text.ParamName, conversation.ParamName, document.ParamName, sequence.ParamName]);
    }

    [Fact]
    public void NothingTheReaderHandsOverIsNull()
    {
        var text = Assert.Throws<ArgumentNullException>(() => new ReadUnit(null!, null, null, null, []));
        var passages = Assert.Throws<ArgumentNullException>(() => new ReadUnit("A", null, null, null, null!));
        var units = Assert.Throws<ArgumentNullException>(() => new ReadMaterial(null!));
        var material = Assert.Throws<ArgumentNullException>(() => ReadOutcome.Read(null!));

        Assert.Equal(["Text", "Passages", "units", "material"], [text.ParamName, passages.ParamName, units.ParamName, material.ParamName]);
    }

    [Fact]
    public void AReadOutcomeIsTheMaterialOrTheReasonAndNeverBoth()
    {
        var read = Units.Read(Units.Plain("A"));
        var unreadable = ReadOutcome.Unreadable(UnreadableReason.Truncated);

        Assert.Equal((true, null, null, UnreadableReason.Truncated), (read.Material is not null, read.Reason, unreadable.Material, unreadable.Reason));
    }

    [Fact]
    public void AReasonOutsideTheEnumerationIsADefectOfTheReader()
    {
        var reason = Assert.Throws<ArgumentOutOfRangeException>(() => ReadOutcome.Unreadable((UnreadableReason)99));

        Assert.Equal("reason", reason.ParamName);
        Assert.StartsWith("The reason is not one of the enumeration.", reason.Message, StringComparison.Ordinal);
    }
}
