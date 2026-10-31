using Core.Persistence.People;
using Core.Persistence.Tests.ServerGuarantees;

namespace Core.Persistence.Tests.People;

/// <summary>
/// What the registry refuses it refuses before it opens a connection, so the data source here points at a port nobody listens on:
/// an answer that is a refusal and not a connection failure proves that nothing was written.
/// </summary>
public sealed class PersonRegistryRefusalTests
{
    private const string Name = "zqx-pessoa-1";

    [Fact]
    public async Task FunctionOutsideThePolicyIsRefusedByNamingTheOneAskedAndTheSixAccepted()
    {
        await using var server = new UnreachableServer();

        var result = await new PersonRegistry(server.DataSource).CreateAsync(new NewPerson(Name, "Zqx-Funcao"), actorId: null, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.FunctionNotAccepted, result.Outcome);
        Assert.Contains("'Zqx-Funcao'", result.Refusal, StringComparison.Ordinal);
        Assert.All(PersonFunctions.Accepted, function => Assert.Contains(function, result.Refusal, StringComparison.Ordinal));
        Assert.Null(result.PersonId);
    }

    [Fact]
    public async Task ChangeToAFunctionOutsideThePolicyIsRefusedWithTheSameReason()
    {
        await using var server = new UnreachableServer();

        var result = await new PersonRegistry(server.DataSource).ChangeFunctionAsync(1, "Zqx-Funcao", actorId: null, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.FunctionNotAccepted, result.Outcome);
        Assert.Contains("'Zqx-Funcao'", result.Refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" zqx-pessoa-1")]
    [InlineData("zqx-pessoa-1 ")]
    [InlineData("zqx-pessoa-1\n")]
    public async Task NameThatIsEmptyOrPaddedIsRefused(string name)
    {
        await using var server = new UnreachableServer();

        var result = await new PersonRegistry(server.DataSource).CreateAsync(new NewPerson(name, PersonFunctions.Accepted[0]), actorId: null, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NameInvalid, result.Outcome);
        Assert.NotNull(result.Refusal);
    }

    [Fact]
    public async Task InvalidNameIsJudgedBeforeTheFunction()
    {
        await using var server = new UnreachableServer();

        var result = await new PersonRegistry(server.DataSource).CreateAsync(new NewPerson("", "Zqx-Funcao"), actorId: null, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NameInvalid, result.Outcome);
    }

    [Fact]
    public async Task NoPersonIsAnError()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.CreateAsync(null!, actorId: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReachOfAPersonWithNoGrantIsAnEmptyListAndNotAnError()
    {
        var reach = PersonRegistry.ReachOf(1);

        Assert.Empty(reach.DomainIds);
    }

    [Fact]
    public void ReachOfAnIdentifierThatIsNotPositiveIsAnError() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PersonRegistry.ReachOf(0));
}
