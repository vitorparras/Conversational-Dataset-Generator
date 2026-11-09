using Core.Persistence.Content.Emptying;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// What the screens of the emptying ask of the server (#617): the domain by its name, the record of an emptied item
/// and the emptying with the person of the session, the motive and the protocol.
/// </summary>
[Trait("Category", "Database")]
public sealed class ItemEmptyingDeskTests
{
    private const string Protocol = "PROT-2026/0042";

    [DatabaseFact]
    public async Task DomainIsFoundByItsNameAndOnlyByIt()
    {
        await using var world = await EmptyingWorld.CreateAsync();

        var known = await world.Desk.DomainIdOfAsync("zqx-domain-b", TestContext.Current.CancellationToken);
        var unknown = await world.Desk.DomainIdOfAsync("zqx-domain-c", TestContext.Current.CancellationToken);
        var wrongCase = await world.Desk.DomainIdOfAsync("ZQX-DOMAIN-B", TestContext.Current.CancellationToken);

        Assert.Equal((2, null, null), (known, unknown, wrongCase));
    }

    [DatabaseFact]
    public async Task ItemWithContentHasNoRecordAndAfterTheEmptyingTheRecordNamesTheExecutorTheMotiveAndTheProtocol()
    {
        await using var world = await EmptyingWorld.CreateAsync();
        var before = await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, TestContext.Current.CancellationToken);

        var emptied = await world.EmptyAsync(world.Curator, EmptyingMotive.RemovalRequestedBySubject, Protocol);
        var after = await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, TestContext.Current.CancellationToken);

        Assert.Equal(
            (null, EmptyingOutcome.Emptied, "zqx-curator", EmptyingMotive.RemovalRequestedBySubject, Protocol),
            (before, emptied.Outcome, after?.Executor, after?.Motive, after?.Reference));
    }

    [DatabaseFact]
    public async Task SecondEmptyingIsToldAsAlreadyEmptiedAndTheRecordIsTheFirstOne()
    {
        await using var world = await EmptyingWorld.CreateAsync();
        _ = await world.EmptyAsync(world.Curator, EmptyingMotive.ContractRequirement, Protocol);

        var again = await world.EmptyAsync(world.Curator, EmptyingMotive.RemovalRequestedBySubject, "OUTRO-1");
        var record = await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.AlreadyEmptied, EmptyingMotive.ContractRequirement, Protocol), (again.Outcome, record?.Motive, record?.Reference));
    }

    [DatabaseFact]
    public async Task ItemOfAnotherDomainIsNotFoundAndNothingIsEmptied()
    {
        await using var world = await EmptyingWorld.CreateAsync();

        var result = await world.EmptyAsync(world.Curator, EmptyingMotive.ContractRequirement, Protocol, domain: 2);
        var record = await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.NotFound, null), (result.Outcome, record));
    }

    [DatabaseFact]
    public async Task PersonWithoutTheFunctionIsRefusedAndAProtocolThatIsNotANumberIsRefusedBeforeAnything()
    {
        await using var world = await EmptyingWorld.CreateAsync();

        var withoutFunction = await world.EmptyAsync(world.Annotator, EmptyingMotive.ContractRequirement, Protocol);
        var badProtocol = await world.EmptyAsync(world.Curator, EmptyingMotive.ContractRequirement, "Maria da Silva");
        var record = await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.AuthorWithoutFunction, EmptyingOutcome.ReferenceRefused, null), (withoutFunction.Outcome, badProtocol.Outcome, record));
    }

    [DatabaseFact]
    public async Task EmptyingWithoutARequestIsRefused()
    {
        await using var world = await EmptyingWorld.CreateAsync();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => world.Desk.EmptyAsync(world.Curator, null!, TestContext.Current.CancellationToken));
    }
}
