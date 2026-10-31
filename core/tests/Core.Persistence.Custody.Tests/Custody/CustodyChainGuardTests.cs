using Core.Persistence.Custody;
using Core.Persistence.People;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>What the chain and the answer of a person's change refuse before any server is asked.</summary>
public sealed class CustodyChainGuardTests
{
    [Fact]
    public void ChainWithoutADataSourceIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => new CustodyChain(null!));

    [Fact]
    public async Task EventThatIsNotGivenIsRefusedBeforeAnythingIsOpened()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=none");
        var chain = new CustodyChain(dataSource);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => chain.AppendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(CustodyOutcome.Conflict)]
    [InlineData(CustodyOutcome.CorrectedNotFound)]
    public void ConflictOfTheChainIsToldToThePersonAsTheChainBeingBusy(CustodyOutcome outcome) =>
        Assert.Equal(PersonOutcome.ChainBusy, PersonRefusals.Chained(CustodyResult.Refused(outcome, "texto"), 3).Outcome);

    [Fact]
    public void AppendedEventIsDoneForThePerson()
    {
        var answer = PersonRefusals.Chained(CustodyResult.Appended(8), 3);

        Assert.Equal((PersonOutcome.Done, 3L), (answer.Outcome, answer.PersonId));
    }

    [Theory]
    [InlineData(CustodyOutcome.ActorUnknown, PersonOutcome.ActorUnknown)]
    [InlineData(CustodyOutcome.ActorDeactivated, PersonOutcome.ActorDeactivated)]
    public void RefusalOfTheAuthorIsTheSameForThePersonAsForTheChain(CustodyOutcome outcome, PersonOutcome expected) =>
        Assert.Equal(expected, PersonRefusals.Chained(CustodyResult.Refused(outcome, "texto"), 3).Outcome);

    [Fact]
    public void OutcomeNobodyDefinedIsADefect() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PersonRefusals.Chained(CustodyResult.Refused((CustodyOutcome)int.MaxValue, "texto"), 3));
}
