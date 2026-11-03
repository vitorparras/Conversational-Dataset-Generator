using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Recovery;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Recovery;

/// <summary>The query the gate of the ingestion makes (issue 67, CA-2): while no rehearsal of the recovery is recorded it says what is missing, and once the step is in the chain it says nothing is.</summary>
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
        var written = await new CustodyChain(world.DataSource).AppendAsync(CustodyEvent.RecoveryTested(1), TestContext.Current.CancellationToken);
        Assert.Equal(CustodyOutcome.Appended, written.Outcome);

        var status = await RecoveryGate.StatusAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((true, null), (status.Tested, status.Missing));
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
