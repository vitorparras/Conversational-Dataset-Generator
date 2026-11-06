using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Recovery;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Recovery;

/// <summary>
/// The query the gate of the ingestion makes (issue 67, CA-2; issue 584): while no rehearsal of the recovery is recorded after the last loss of the master key it says what is missing,
/// and once the step is in the chain it says nothing is.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecoveryGateTests
{
    [DatabaseFact]
    public async Task Ca2WhileThereIsNoRecordedRehearsalTheGateSaysWhatIsMissingAndNamesTheTwoCommands()
    {
        await using var world = await ContentWorld.CreateAsync();

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((false, RecoveryGate.MissingWords), (status.Tested, status.Missing));
        Assert.All(["rehearse-recovery", "record-recovery-test", "operacao/004"], part => Assert.Contains(part, status.Missing!, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task Ca2AChainWithOtherStepsOnlyStillSaysWhatIsMissing()
    {
        await using var world = await ContentWorld.CreateAsync();
        var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        _ = await new CustodyChain(world.DataSource).AppendAsync(added, TestContext.Current.CancellationToken);

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.False(status.Tested);
    }

    [DatabaseFact]
    public async Task Ca2OnceTheStepIsInTheChainTheGateSaysItIsTestedAndNothingIsMissing()
    {
        await using var world = await ContentWorld.CreateAsync();
        var written = await new CustodyChain(world.DataSource).AppendAsync(CustodyEvent.RecoveryRehearsed(1), TestContext.Current.CancellationToken);
        Assert.Equal(CustodyOutcome.Appended, written.Outcome);

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((true, null), (status.Tested, status.Missing));
    }

    [DatabaseFact]
    public async Task Ca584ARehearsalBeforeTheLastLossOfTheMasterKeyDoesNotOpenTheGateAndTheWordsSayToRepeatIt()
    {
        await using var world = await ContentWorld.CreateAsync();
        await AppendAsync(world, CustodyEvent.RecoveryRehearsed(1));
        await DeclareAsync(world, KeyLossScope.MasterText);

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((false, RecoveryGate.StaleWords), (status.Tested, status.Missing));
        Assert.All(
            ["anterior à última perda declarada da chave mestra", "rehearse-recovery", "record-recovery-test", "operacao/004"],
            part => Assert.Contains(part, status.Missing!, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task Ca584ARehearsalAfterTheLastLossOfTheMasterKeyOpensTheGateAndAnotherLossClosesItAgain()
    {
        await using var world = await ContentWorld.CreateAsync();
        await AppendAsync(world, CustodyEvent.RecoveryRehearsed(1));
        await DeclareAsync(world, KeyLossScope.MasterText);
        await AppendAsync(world, CustodyEvent.RecoveryRehearsed(2));
        var opened = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);
        await DeclareAsync(world, KeyLossScope.MasterText);

        var closed = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal(((true, null), (false, RecoveryGate.StaleWords)), ((opened.Tested, opened.Missing), (closed.Tested, closed.Missing)));
    }

    [DatabaseFact]
    public async Task Ca584ALossDeclaredWithNoRehearsalAtAllSaysTheRehearsalIsMissing()
    {
        await using var world = await ContentWorld.CreateAsync();
        await DeclareAsync(world, KeyLossScope.MasterText);

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((false, RecoveryGate.MissingWords), (status.Tested, status.Missing));
    }

    [DatabaseFact]
    public async Task Ca584TheLossOfTheKeyOfOneLabelDoesNotCloseTheGate()
    {
        await using var world = await ContentWorld.CreateAsync();
        await AppendAsync(world, CustodyEvent.RecoveryRehearsed(1));
        await DeclareAsync(world, "texto-bruto/1/v1");

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((true, null), (status.Tested, status.Missing));
    }

    [DatabaseFact]
    public async Task Ca584TheOrderOfTheChainDecidesAndNotTheClockOfTheRehearsalThatIsLate()
    {
        await using var world = await ContentWorld.CreateAsync();
        await DeclareAsync(world, KeyLossScope.MasterText);
        _ = await world.OwnerInDomainAsync(1, Backdated("recovery_tested", 2, "NULL"));

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((true, null), (status.Tested, status.Missing));
    }

    [DatabaseFact]
    public async Task Ca584ALossWithAnEarlierClockButLaterInTheChainStillClosesTheGate()
    {
        await using var world = await ContentWorld.CreateAsync();
        await AppendAsync(world, CustodyEvent.RecoveryRehearsed(1));
        _ = await world.OwnerInDomainAsync(1, Backdated("key_loss_declared", 3, "'master'"));

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((false, RecoveryGate.StaleWords), (status.Tested, status.Missing));
    }

    private static string Backdated(string step, int item, string reference) =>
        "INSERT INTO public.custody_event (occurred_at, actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + $"VALUES (now() - interval '2 days', NULL, true, 'master_key', {item}, '{step}', 'administrative_act', {reference})";

    private static async Task AppendAsync(ContentWorld world, CustodyEvent custodyEvent) =>
        Assert.Equal(CustodyOutcome.Appended, (await new CustodyChain(world.DataSource).AppendAsync(custodyEvent, TestContext.Current.CancellationToken)).Outcome);

    private static async Task DeclareAsync(ContentWorld world, string scope)
    {
        Assert.True(KeyLossScope.TryRead(scope, out var read));
        await AppendAsync(world, CustodyEvent.KeyLossDeclared(read!, KeyLossAttempts.SafekeepingCopy));
    }

    [Fact]
    public async Task AGateWithoutASourceIsADefectOfTheCaller() =>
        Assert.Equal("dataSource", (await Assert.ThrowsAsync<ArgumentNullException>(() => RecoveryGate.StatusAsync(null!, TestContext.Current.CancellationToken))).ParamName);

    [Fact]
    public async Task ADatabaseThatDoesNotAnswerIsAnErrorOfTheDatabaseAndNotAGateThatSaysTested()
    {
        await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d;Timeout=2;Pooling=false");

        _ = await Assert.ThrowsAnyAsync<NpgsqlException>(() => RecoveryGate.StatusAsync(source, TestContext.Current.CancellationToken));
    }
}
