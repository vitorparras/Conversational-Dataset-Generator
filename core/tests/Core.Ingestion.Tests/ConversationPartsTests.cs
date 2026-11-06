using Core.Boundary.Text;

namespace Core.Ingestion.Tests;

/// <summary>The parts of a conversation accepted (issue 77, criterion 4): each carries its author and its rank in the order, and none is lost or merged.</summary>
public sealed class ConversationPartsTests
{
    private static readonly FormConference Conference = new(new TextBoundaries());

    private static ReadUnit Unit(string text, string author, int second, int? answers) => new(text, author, Units.First.AddSeconds(second), answers, []);

    private static IReadOnlyList<Message> Parts(params ReadUnit[] units) =>
        Assert.IsType<Conversation>(Conference.Confer(MaterialForm.Conversation, Units.Read(units)).Accepted).Messages;

    [Fact]
    public void EveryPartCarriesItsAuthorAndItsRankInTheOrderOfTheMoments()
    {
        var parts = Parts(Unit("TRES", "cai", 30, null), Unit("UM", "ana", 10, null), Unit("DOIS", "bia", 20, null));

        Assert.Equal(
            [("ana", 0, "UM"), ("bia", 1, "DOIS"), ("cai", 2, "TRES")],
            [.. parts.Select(part => (part.Author, part.Rank, part.Text))]);
    }

    [Fact]
    public void TheNumberOfPartsIsTheNumberOfMessagesReadEvenWhenTheyShareAuthorTextAndMoment()
    {
        var parts = Parts(Unit("IGUAL", "ana", 5, null), Unit("IGUAL", "ana", 6, null), Unit("IGUAL", "ana", 7, null), Unit("IGUAL", "ana", 7, 2));

        Assert.Equal(4, parts.Count);
        Assert.Equal([0, 1, 2, 3], [.. parts.Select(part => part.Rank)]);
    }

    [Fact]
    public void TheRankOfAPartIsItsIndexAndTheMessageAnsweredIsGivenByThePlaceOfThatMessage()
    {
        var parts = Parts(Unit("RESPOSTA", "bia", 9, 1), Unit("PERGUNTA", "ana", 4, null));

        Assert.Equal(
            [(0, "PERGUNTA", null), (1, "RESPOSTA", 0)],
            [.. parts.Select((part, index) => (part.Rank == index ? part.Rank : -1, part.Text, part.RepliesTo))]);
    }

    [Fact]
    public void TheAuthorOfAPartIsTheOneOfTheMessageItCameFromAndNeverTheOneOfTheNeighbour()
    {
        var parts = Parts(Unit("B", "bia", 2, null), Unit("A", "ana", 1, null), Unit("B2", "bia", 3, 0));

        Assert.Equal(["ana", "bia", "bia"], [.. parts.Select(part => part.Author)]);
    }
}
