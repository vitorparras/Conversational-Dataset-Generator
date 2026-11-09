using Core.Persistence.Ingestion;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The conditions the installation knows, from the most restrictive to the most permissive: the place in the list is the order,
/// and a list that cannot be run with is refused (issue 85, CA-1 and CA-4).
/// </summary>
public sealed class OriginConditionsTests
{
    private const string NotAName = "uma condição não é um nome de letras minúsculas, algarismos e sublinhado";

    [Fact]
    public void TheListKeepsTheOrderGivenAndTheMostRestrictiveIsTheFirst()
    {
        var conditions = OriginConditions.Of(["sigilosa", "restrita", "livre"]);

        Assert.Equal(["sigilosa", "restrita", "livre"], conditions.Names);
        Assert.Equal("sigilosa", conditions.MostRestrictive);
        Assert.Equal([0, 1, 2], [conditions.RankOf("sigilosa"), conditions.RankOf("restrita"), conditions.RankOf("livre")]);
    }

    [Fact]
    public void AConditionTheInstallationDoesNotKnowHasNoPlace()
    {
        var conditions = OriginConditions.Of(["a"]);

        Assert.Equal([-1, -1, -1], [conditions.RankOf("b"), conditions.RankOf(string.Empty), conditions.RankOf("A")]);
    }

    [Fact]
    public void AListWithOneConditionIsTheMostRestrictiveAndTheMostPermissiveAtOnce() =>
        Assert.Equal("unica", OriginConditions.Of(["unica"]).MostRestrictive);

    [Fact]
    public void TheListIsCopiedSoThatChangingTheOneGivenChangesNothing()
    {
        var given = new[] { "restrita", "livre" };
        var conditions = OriginConditions.Of(given);

        given[0] = "livre";

        Assert.Equal("restrita", conditions.MostRestrictive);
    }

    [Fact]
    public void AnEmptyListIsRefusedWithTheReason()
    {
        Assert.Equal("a lista de condições está vazia", OriginConditions.Refusal([]));
        Assert.Equal("ordered", Assert.Throws<ArgumentException>(() => OriginConditions.Of([])).ParamName);
    }

    [Fact]
    public void ANameThatRepeatsIsRefusedWithTheReasonWhereverTheRepetitionIs() =>
        Assert.Equal(
            ["uma condição se repete", "uma condição se repete"],
            [OriginConditions.Refusal(["restrita", "restrita"]), OriginConditions.Refusal(["a", "b", "a"])]);

    [Fact]
    public void NamesThatDifferOnlyInCaseAreRefusedBecauseNoneWithCapitalsIsAName() =>
        Assert.Equal(NotAName, OriginConditions.Refusal(["a", "A"]));

    [Fact]
    public void AnEmptyPlaceIsNotANameAndIsRefusedBeforeTheRepetition() =>
        Assert.Equal(NotAName, OriginConditions.Refusal(["a", string.Empty, string.Empty]));

    [Fact]
    public void AValidListHasNoRefusal() =>
        Assert.Null(OriginConditions.Refusal(["restrita", "intermediaria_2", "livre"]));

    [Theory]
    [InlineData("a", true)]
    [InlineData("restrita", true)]
    [InlineData("restrita_2", true)]
    [InlineData("a0", true)]
    [InlineData("a_", true)]
    [InlineData("", false)]
    [InlineData("A", false)]
    [InlineData("Restrita", false)]
    [InlineData("1restrita", false)]
    [InlineData("_restrita", false)]
    [InlineData("com espaco", false)]
    [InlineData("com-traco", false)]
    [InlineData("acentuação", false)]
    [InlineData("com.ponto", false)]
    public void ANameIsALowercaseLetterThenLowercaseLettersDigitsAndUnderscores(string candidate, bool expected) =>
        Assert.Equal(expected, OriginConditions.IsName(candidate));

    [Fact]
    public void ANameHasAtMostSixtyThreeCharactersAsTheDatabaseHoldsIt() =>
        Assert.Equal(
            (true, false, false),
            (OriginConditions.IsName(new string('a', 63)), OriginConditions.IsName(new string('a', 64)), OriginConditions.IsName(null)));

    [Fact]
    public void MissingListsAreRefusedByName() =>
        Assert.Equal(
            ["ordered", "ordered"],
            [Assert.Throws<ArgumentNullException>(() => OriginConditions.Of(null!)).ParamName, Assert.Throws<ArgumentNullException>(() => OriginConditions.Refusal(null!)).ParamName]);
}
