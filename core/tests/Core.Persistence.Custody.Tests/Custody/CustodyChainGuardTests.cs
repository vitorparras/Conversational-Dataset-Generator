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

    [Fact]
    public async Task CreatingWithoutSayingWhoActsIsRefusedBeforeAnythingIsOpened()
    {
        await using var dataSource = Unreachable();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => new PersonRegistry(dataSource).CreateAsync(new NewPerson("zqx-pessoa", "Anotação"), null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeactivatingWithoutSayingWhoActsIsRefusedBeforeAnythingIsOpened()
    {
        await using var dataSource = Unreachable();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => new PersonRegistry(dataSource).DeactivateAsync(1, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChangingTheFunctionWithoutSayingWhoActsIsRefusedBeforeAnythingIsOpened()
    {
        await using var dataSource = Unreachable();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => new PersonRegistry(dataSource).ChangeFunctionAsync(1, "Anotação", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SettingAPasswordWithoutSayingWhoActsIsRefusedBeforeAnythingIsOpened()
    {
        await using var dataSource = Unreachable();

        var credentials = new PersonCredentials(dataSource, new PasswordDerivation(2));

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => credentials.SetPasswordAsync(1, "ZQX-SENHA-5820", null!, TestContext.Current.CancellationToken));
    }

    /// <summary>A data source nothing answers at: whatever reached it would fail on the connection, so a refusal of the argument proves nothing went further.</summary>
    private static NpgsqlDataSource Unreachable() => NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=none;Timeout=1");
}
