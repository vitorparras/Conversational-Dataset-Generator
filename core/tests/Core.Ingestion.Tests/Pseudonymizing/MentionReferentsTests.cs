using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// The group the service gave a mention becomes the referent of the swap: a mention linked to an author refers to the author, and every other group refers to itself, so that the
/// nickname of an author, which the name rule leaves in a group of its own, never takes the marker of the author (decision 0060).
/// </summary>
public sealed class MentionReferentsTests
{
    private static readonly AuthorGroup[] Authors = [new AuthorGroup("ZQXUM", 1), new AuthorGroup("ZQXDOIS", 3)];

    [Fact]
    public void MentionInTheGroupOfAnAuthorRefersToTheAuthor()
    {
        var referred = MentionReferents.Refer(Mention(3), Authors);

        Assert.Equal(Referent.Author("ZQXDOIS"), referred.Referent);
    }

    [Fact]
    public void MentionInAGroupOfNoAuthorRefersToItsGroup()
    {
        var referred = MentionReferents.Refer(Mention(2), Authors);

        Assert.Equal(Referent.Group(2), referred.Referent);
    }

    [Fact]
    public void NicknameInAGroupOfItsOwnNeverRefersToTheAuthor()
    {
        var author = MentionReferents.Refer(Mention(1), Authors);
        var nickname = MentionReferents.Refer(Mention(4), Authors);

        Assert.NotEqual(author.Referent, nickname.Referent);
    }

    [Fact]
    public void TheReferredOccurrenceKeepsTheOccurrenceAndItsDestination()
    {
        var mention = Mention(2);

        Assert.Same(mention, MentionReferents.Refer(mention, Authors).Decided);
    }

    [Fact]
    public void MentionWithNoGroupIsADefect()
    {
        var ungrouped = new DecidedOccurrence(new Occurrence(0, 4, OccurrenceKinds.Person, "PROPN", "nsubj"), Destination.Pseudonymize);

        var defect = Assert.Throws<ArgumentException>(() => MentionReferents.Refer(ungrouped, Authors));

        Assert.Equal("decided", defect.ParamName);
    }

    [Fact]
    public void NoMentionAndNoAuthorsAreDefects()
    {
        Assert.Equal("decided", Assert.Throws<ArgumentNullException>(() => MentionReferents.Refer(null!, Authors)).ParamName);
        Assert.Equal("authors", Assert.Throws<ArgumentNullException>(() => MentionReferents.Refer(Mention(1), null!)).ParamName);
    }

    private static DecidedOccurrence Mention(int group) =>
        new(new Occurrence(0, 4, OccurrenceKinds.Person, "PROPN", "nsubj"), Destination.Pseudonymize) { Group = group };
}
