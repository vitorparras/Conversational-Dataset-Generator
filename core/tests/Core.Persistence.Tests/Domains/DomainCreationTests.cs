using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Domains;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Domains;

/// <summary>
/// The creation of a domain (issue 54, part 2), over a migrated database of its own and the credential of the owner: the domain, its two derivation labels and the event of the chain
/// exist together or not at all (CA-4a to CA-4e).
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainCreationTests
{
    private const string Domains = "SELECT count(*) FROM public.domain";
    private const string Labels = "SELECT count(*) FROM public.derivation_label";
    private const string Events = "SELECT count(*) FROM public.custody_event";
    private const string Created = "SELECT count(*) FROM public.custody_event WHERE step = 'domain_created'";

    private static readonly DerivationLabels First = DerivationLabels.Of(["texto-bruto/1/v1", "mapa-reversao/1/v1"]);
    private static readonly DomainCreationOutcome[] RefusedWhenSecond = [DomainCreationOutcome.Conflict, DomainCreationOutcome.LabelsMissing];

    [DatabaseFact]
    public async Task Ca4aBothLabelsInTheCustodyCreateTheDomainWithTheAnnouncedIdTheTwoLabelsAndTheEvent()
    {
        await using var world = await World.CreateAsync();

        var result = await world.Creation.CreateAsync("zqx-domain-a", First, TestContext.Current.CancellationToken);

        Assert.Equal((DomainCreationOutcome.Created, 1), (result.Outcome, result.DomainId));
        Assert.Equal(["mapa-reversao/1/v1", "texto-bruto/1/v1"], await world.Database.RecordedLabelsAsOwnerAsync());
        Assert.Equal(
            "system,domain,1,domain_created,administrative_act",
            await world.TextAsync("SELECT concat_ws(',', CASE WHEN actor_system THEN 'system' END, item_kind, item_id, step, reason) FROM public.custody_event"));
        Assert.Equal(1L, await world.CountAsync(Created));
        Assert.Equal((long)result.Sequence!, await world.CountAsync("SELECT sequence FROM public.custody_event WHERE step = 'domain_created'"));
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.True(verified.Passed);
    }

    [DatabaseFact]
    public async Task Ca4aTheNextDomainTakesTheNextIdAndADomainAddedWithoutAnIdDoesNotTakeOneUsed()
    {
        await using var world = await World.CreateAsync();
        var both = DerivationLabels.Of(["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"]);

        var first = await world.Creation.CreateAsync("zqx-domain-a", both, TestContext.Current.CancellationToken);
        var second = await world.Creation.CreateAsync("zqx-domain-b", both, TestContext.Current.CancellationToken);
        await world.Database.RunAsOwnerAsync("INSERT INTO public.domain (name) VALUES ('zqx-domain-c')");

        Assert.Equal((1, 2), (first.DomainId, second.DomainId));
        Assert.Equal(3L, await world.CountAsync("SELECT id FROM public.domain WHERE name = 'zqx-domain-c'"));
    }

    [DatabaseFact]
    public async Task Ca4bALabelMissingRefusesNamesItWithTheIdAndLeavesTheDatabaseAndTheChainAsTheyWere()
    {
        await using var world = await World.CreateAsync();
        var onlyTheText = DerivationLabels.Of(["texto-bruto/1/v1"]);

        var result = await world.Creation.CreateAsync("zqx-domain-a", onlyTheText, TestContext.Current.CancellationToken);

        Assert.Equal((DomainCreationOutcome.LabelsMissing, 1), (result.Outcome, result.DomainId));
        Assert.Equal(["mapa-reversao/1/v1"], result.MissingLabels);
        Assert.Equal((0L, 0L, 0L), await world.CountsAsync());
    }

    [DatabaseFact]
    public async Task Ca4bNoLabelAtAllNamesBothInOrder()
    {
        await using var world = await World.CreateAsync();

        var result = await world.Creation.CreateAsync("zqx-domain-a", DerivationLabels.None, TestContext.Current.CancellationToken);

        Assert.Equal(["mapa-reversao/1/v1", "texto-bruto/1/v1"], result.MissingLabels);
        Assert.Equal((0L, 0L, 0L), await world.CountsAsync());
    }

    [DatabaseFact]
    public async Task Ca4bTheLabelsOfAnotherDomainDoNotCoverTheNextOne()
    {
        await using var world = await World.CreateAsync();
        _ = await world.Creation.CreateAsync("zqx-domain-a", First, TestContext.Current.CancellationToken);

        var result = await world.Creation.CreateAsync("zqx-domain-b", First, TestContext.Current.CancellationToken);

        Assert.Equal((DomainCreationOutcome.LabelsMissing, 2), (result.Outcome, result.DomainId));
        Assert.Equal(["mapa-reversao/2/v1", "texto-bruto/2/v1"], result.MissingLabels);
        Assert.Equal((1L, 2L, 1L), await world.CountsAsync());
    }

    [DatabaseFact]
    public async Task Ca4cARepeatedNameIsRefusedWithoutWritingAnything()
    {
        await using var world = await World.CreateAsync();
        var both = DerivationLabels.Of(["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"]);
        _ = await world.Creation.CreateAsync("zqx-domain-a", both, TestContext.Current.CancellationToken);

        var result = await world.Creation.CreateAsync("zqx-domain-a", both, TestContext.Current.CancellationToken);

        Assert.Equal(DomainCreationOutcome.NameTaken, result.Outcome);
        Assert.Equal((1L, 2L, 1L), await world.CountsAsync());
    }

    [DatabaseTheory]
    [InlineData("")]
    [InlineData(" zqx-domain-a")]
    [InlineData("zqx-domain-a ")]
    public async Task Ca4cANameEmptyOrPaddedIsRefusedWithoutWritingAnything(string name)
    {
        await using var world = await World.CreateAsync();

        var result = await world.Creation.CreateAsync(name, First, TestContext.Current.CancellationToken);

        Assert.Equal(DomainCreationOutcome.NameInvalid, result.Outcome);
        Assert.Equal((0L, 0L, 0L), await world.CountsAsync());
    }

    [DatabaseFact]
    public async Task Ca4dAFailureInTheEventLeavesNeitherTheDomainNorTheLabels()
    {
        await using var world = await World.CreateAsync();
        var failing = new DomainCreation(world.DataSource, (_, _, _) => throw new TimeoutException("zqx-injected"));

        _ = await Assert.ThrowsAsync<TimeoutException>(() => failing.CreateAsync("zqx-domain-a", First, TestContext.Current.CancellationToken));

        Assert.Equal((0L, 0L, 0L), await world.CountsAsync());
        Assert.Equal(DomainCreationOutcome.Created, (await world.Creation.CreateAsync("zqx-domain-a", First, TestContext.Current.CancellationToken)).Outcome);
    }

    [DatabaseFact]
    public async Task Ca4dAChainThatRefusesTheEventLeavesNeitherTheDomainNorTheLabels()
    {
        await using var world = await World.CreateAsync();
        var chain = new CustodyChain(world.DataSource);
        var earlier = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(earlier, TestContext.Current.CancellationToken)).Outcome);
        var failed = CustodyEvent.Verification(eventsRead: 1, firstInconsistent: 1);
        Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(failed, TestContext.Current.CancellationToken)).Outcome);

        var result = await world.Creation.CreateAsync("zqx-domain-a", First, TestContext.Current.CancellationToken);

        Assert.Equal((DomainCreationOutcome.ChainRefused, CustodyOutcome.ChainBroken), (result.Outcome, result.ChainOutcome));
        Assert.Equal((0L, 0L, 2L), await world.CountsAsync());
    }

    [DatabaseFact]
    public async Task Ca4dAFailureInTheLabelLeavesNeitherTheDomainNorTheLabelsBeforeIt()
    {
        await using var world = await World.CreateAsync();
        await world.Database.RunAsOwnerAsync("INSERT INTO public.derivation_label (label) VALUES ('mapa-reversao/1/v1')");

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Creation.CreateAsync("zqx-domain-a", First, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("zqx-domain-a", refused.Message, StringComparison.Ordinal);
        Assert.Equal((0L, 1L, 0L), await world.CountsAsync());
    }

    [DatabaseFact]
    public async Task Ca4eTwoCreationsAtOnceLetOneThroughRefuseTheOtherAndKeepTheChainWhole()
    {
        await using var world = await World.CreateAsync();
        using var start = new ManualResetEventSlim();
        var attempts = Enumerable.Range(0, 2).Select(index => $"zqx-domain-{index}")
            .Select(name => Task.Run(
                () =>
                {
                    start.Wait(TestContext.Current.CancellationToken);
                    return world.Creation.CreateAsync(name, First, TestContext.Current.CancellationToken);
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        start.Set();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(result => result.Outcome == DomainCreationOutcome.Created));
        var refused = results.Single(result => result.Outcome != DomainCreationOutcome.Created);
        Assert.Contains(refused.Outcome, RefusedWhenSecond);
        Assert.Equal((1L, 2L, 1L), await world.CountsAsync());
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.True(verified.Passed);
    }

    [DatabaseFact]
    public async Task Ca4fTheEventCarriesTheNumberOfTheDomainAndNeverItsName()
    {
        await using var world = await World.CreateAsync();
        _ = await world.Creation.CreateAsync("zqx-nome-do-dominio", First, TestContext.Current.CancellationToken);

        var row = await world.TextAsync("SELECT c::text FROM public.custody_event c");

        Assert.DoesNotContain("zqx-nome-do-dominio", row, StringComparison.Ordinal);
    }

    [Fact]
    public void LabelsOfADomainAreOneForEachPurposeInTheFormatOfTheDecision() =>
        Assert.Equal(["texto-bruto/7/v1", "mapa-reversao/7/v1"], DomainCreation.LabelsOf(7));

    [Fact]
    public void ADataSourceThatIsNotGivenIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new DomainCreation(null!));

    [Fact]
    public async Task ANameOrACustodyThatIsNotGivenIsADefect()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-used;Database=d");
        var creation = new DomainCreation(dataSource);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => creation.CreateAsync(null!, First, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => creation.CreateAsync("zqx", null!, TestContext.Current.CancellationToken));
    }

    [SuppressMessage("Design", "CA1812", Justification = "The world is built by its factory.")]
    private sealed class World : IAsyncDisposable
    {
        private World(ScratchDatabase database, NpgsqlDataSource dataSource)
        {
            Database = database;
            DataSource = dataSource;
            Creation = new DomainCreation(dataSource);
        }

        internal ScratchDatabase Database { get; }

        internal NpgsqlDataSource DataSource { get; }

        internal DomainCreation Creation { get; }

        internal static async Task<World> CreateAsync()
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            return new World(database, NpgsqlDataSource.Create(database.OwnerConnectionString));
        }

        internal async Task<long> CountAsync(string statement)
        {
            await using var command = DataSource.CreateCommand(statement);
            return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        }

        internal async Task<string> TextAsync(string statement)
        {
            await using var command = DataSource.CreateCommand(statement);
            return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        }

        /// <summary>The domains, the labels and the events of the chain, in that order.</summary>
        internal async Task<(long, long, long)> CountsAsync() => (await CountAsync(Domains), await CountAsync(Labels), await CountAsync(Events));

        public async ValueTask DisposeAsync()
        {
            await DataSource.DisposeAsync();
            await Database.DisposeAsync();
        }
    }
}
