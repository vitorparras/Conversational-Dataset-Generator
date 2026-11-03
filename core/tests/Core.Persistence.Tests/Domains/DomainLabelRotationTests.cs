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
/// The rotation of the label of a domain (issue 56, part 1), over a migrated database of its own and the credential of the owner: the labels of the next generation and the event
/// of the chain exist together or not at all, another domain is never touched, and no label is changed or removed (CA-1, CA-2, CA-3, CA-5).
/// and no label is changed or removed (CA-1, CA-2, CA-3, CA-5).
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainLabelRotationTests
{
    private const string Labels = "SELECT count(*) FROM public.derivation_label";
    private const string Events = "SELECT count(*) FROM public.custody_event";

    private const string EventOfTheRotation =
        "SELECT concat_ws(',', CASE WHEN actor_system THEN 'system' END, item_kind, item_id, step, reason, reference, occurred_at IS NOT NULL) "
        + "FROM public.custody_event WHERE step = 'domain_label_rotated'";

    private static readonly string[] AllOfBoth =
    [
        "texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1",
        "texto-bruto/1/v2", "mapa-reversao/1/v2", "texto-bruto/1/v3", "mapa-reversao/1/v3",
    ];

    [DatabaseFact]
    public async Task Ca1RotatingAddsTheLabelsOfTheNextGenerationAndKeepsTheOlderOnes()
    {
        await using var world = await World.CreateAsync();

        var result = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.Rotated, 1, 2), (result.Outcome, result.DomainId, result.Version));
        Assert.Equal(
            ["mapa-reversao/1/v1", "mapa-reversao/1/v2", "mapa-reversao/2/v1", "texto-bruto/1/v1", "texto-bruto/1/v2", "texto-bruto/2/v1"],
            await world.Database.RecordedLabelsAsOwnerAsync());
    }

    [DatabaseFact]
    public async Task Ca1TheRegisterTheWriteReadsHoldsTheNewGenerationOnlyAfterTheRotationAndOnlyForItsDomain()
    {
        await using var world = await World.CreateAsync();
        var before = await RegisteredLabels.ReadAsync(world.DataSource, TestContext.Current.CancellationToken);

        _ = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);
        var after = await RegisteredLabels.ReadAsync(world.DataSource, TestContext.Current.CancellationToken);

        Assert.Equal((false, true, false, true), (before.Contains("texto-bruto/1/v2"), after.Contains("texto-bruto/1/v2"), after.Contains("texto-bruto/2/v2"), after.Contains("texto-bruto/1/v1")));
    }

    [DatabaseFact]
    public async Task Ca1RotatingAgainTakesTheGenerationAfterTheLastOne()
    {
        await using var world = await World.CreateAsync();
        var custody = DerivationLabels.Of(AllOfBoth);

        var second = await world.Rotation.RotateAsync(1, custody, TestContext.Current.CancellationToken);
        var third = await world.Rotation.RotateAsync(1, custody, TestContext.Current.CancellationToken);

        Assert.Equal((2, 3, 8L), (second.Version, third.Version, await world.CountAsync(Labels)));
    }

    [DatabaseFact]
    public async Task Ca2RotatingOneDomainChangesNeitherTheLabelsNorTheEventsOfTheOther()
    {
        await using var world = await World.CreateAsync();

        _ = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);

        Assert.Equal(2L, await world.CountAsync("SELECT count(*) FROM public.derivation_label WHERE label LIKE '%/2/v%'"));
        Assert.Equal(
            ("1", "v2", 1L),
            (await world.TextAsync("SELECT item_id::text FROM public.custody_event WHERE step = 'domain_label_rotated'"),
             await world.TextAsync("SELECT reference FROM public.custody_event WHERE step = 'domain_label_rotated'"),
             await world.CountAsync("SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_rotated'")));
        Assert.Equal(0L, await world.CountAsync("SELECT count(*) FROM public.custody_event WHERE item_kind = 'domain' AND item_id = 2 AND step <> 'domain_created'"));
    }

    [DatabaseFact]
    public async Task Ca3ALabelOfTheNextGenerationMissingFromTheCustodyIsNamedAndNothingIsWritten()
    {
        await using var world = await World.CreateAsync();
        var before = (await world.CountAsync(Labels), await world.CountAsync(Events));

        var result = await world.Rotation.RotateAsync(1, DerivationLabels.Of(["texto-bruto/1/v2"]), TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.LabelsMissing, 2), (result.Outcome, result.Version));
        Assert.Equal(["mapa-reversao/1/v2"], result.MissingLabels);
        Assert.Equal(before, (await world.CountAsync(Labels), await world.CountAsync(Events)));
    }

    [DatabaseFact]
    public async Task Ca3ADomainThatDoesNotExistIsRefusedAndNothingIsWritten()
    {
        await using var world = await World.CreateAsync();

        var result = await world.Rotation.RotateAsync(9, DerivationLabels.Of(["texto-bruto/9/v2", "mapa-reversao/9/v2"]), TestContext.Current.CancellationToken);

        Assert.Equal(DomainLabelOutcome.DomainNotFound, result.Outcome);
        Assert.Equal(4L, await world.CountAsync(Labels));
    }

    [DatabaseFact]
    public async Task Ca5TheEventOfARotationSaysWhoWhenWhichDomainAndTheGenerationItPassedToAndTheChainVerifies()
    {
        await using var world = await World.CreateAsync();
        var result = await world.Rotation.RotateAsync(2, DerivationLabels.Of(["texto-bruto/2/v2", "mapa-reversao/2/v2"]), TestContext.Current.CancellationToken);

        Assert.Equal(
            "system,domain,2,domain_label_rotated,administrative_act,v2,t",
            await world.TextAsync(EventOfTheRotation));
        Assert.Equal((long)result.Sequence!, await world.CountAsync("SELECT sequence FROM public.custody_event WHERE step = 'domain_label_rotated'"));
        Assert.True((await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken)).Passed);
    }
    [DatabaseFact]
    public async Task RotationsAtOnceNeverLeaveAGapOrARepeatInTheGenerations()
    {
        await using var world = await World.CreateAsync();
        var custody = DerivationLabels.Of(AllOfBoth);

        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => world.Rotation.RotateAsync(1, custody, TestContext.Current.CancellationToken)));

        var rotated = results.Count(result => result.Outcome == DomainLabelOutcome.Rotated);
        Assert.InRange(rotated, 1, 2);
        Assert.Equal(2L + (2 * rotated), await world.CountAsync("SELECT count(*) FROM public.derivation_label WHERE label LIKE '%/1/v%'"));
        Assert.Equal(rotated, await world.CountAsync("SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_rotated'"));
    }

    [DatabaseFact]
    public async Task Ca4aALabelThatAnotherRotationWroteFirstIsAConflictAndNothingOfThisOneIsKept()
    {
        await using var world = await World.CreateAsync();
        await TakeTheLabelAsync(world, "unique_violation");

        var result = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);

        Assert.Equal(DomainLabelOutcome.Conflict, result.Outcome);
        Assert.Equal((4L, 2L), (await world.CountAsync(Labels), await world.CountAsync(Events)));
    }

    [DatabaseFact]
    public async Task Ca4bAFailureOfTheLabelThatIsNotAClashIsNotTakenForAConflict()
    {
        await using var world = await World.CreateAsync();
        await TakeTheLabelAsync(world, "raise_exception");

        _ = await Assert.ThrowsAnyAsync<Exception>(() => world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken));

        Assert.Equal((4L, 2L), (await world.CountAsync(Labels), await world.CountAsync(Events)));
    }

    [DatabaseFact]
    public async Task Ca4cAChainThatRefusesTheEventLeavesNoLabelAndTheRefusalIsTheChains()
    {
        await using var world = await World.CreateAsync();
        var failed = CustodyEvent.Verification(eventsRead: 1, firstInconsistent: 1);
        Assert.Equal(CustodyOutcome.Appended, (await new CustodyChain(world.DataSource).AppendAsync(failed, TestContext.Current.CancellationToken)).Outcome);

        var result = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.ChainRefused, 4L), (result.Outcome, await world.CountAsync(Labels)));
    }

    [DatabaseFact]
    public async Task Ca4dAWriterThatMetAnotherAtTheSameLinkOfTheChainIsAConflictAndLeavesNoLabel()
    {
        await using var world = await World.CreateAsync();
        var meeting = new DomainLabelRotation(world.DataSource, (_, _, _) => Task.FromResult(new CustodyResult { Outcome = CustodyOutcome.Conflict }));

        var result = await meeting.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.Conflict, 4L), (result.Outcome, await world.CountAsync(Labels)));
    }

    [Fact]
    public async Task TheRotationNeedsItsDataSourceItsWayOfAddingAndTheLabelsOfTheCustody()
    {
        await using var source = NpgsqlDataSource.Create("Host=zqx-never-connected");
        var rotation = new DomainLabelRotation(source);

        Assert.Equal(
            ["dataSource", "dataSource", "append", "custody"],
            [
                Assert.Throws<ArgumentNullException>(() => new DomainLabelRotation(null!)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new DomainLabelRotation(null!, (_, _, _) => throw new TimeoutException())).ParamName,
                Assert.Throws<ArgumentNullException>(() => new DomainLabelRotation(source, null!)).ParamName,
                (await Assert.ThrowsAsync<ArgumentNullException>(() => rotation.RotateAsync(1, null!, TestContext.Current.CancellationToken))).ParamName,
            ]);
    }

    [Fact]
    public void TheLabelsOfAGenerationAreOneForEachPurposeInTheOrderOfThePurposes() =>
        Assert.Equal(["texto-bruto/7/v3", "mapa-reversao/7/v3"], DomainLabelRotation.LabelsOf(7, 3));

    [Fact]
    public async Task TheRegisterOfLabelsIsReadOnlyFromADataSource() =>
        Assert.Equal("dataSource", (await Assert.ThrowsAsync<ArgumentNullException>(() => RegisteredLabels.ReadAsync(null!, TestContext.Current.CancellationToken))).ParamName);

    /// <summary>Makes the registration of one label fail with the error given, as it does for the rotation that came second.</summary>
    private static async Task TakeTheLabelAsync(World world, string errorCode)
    {
        await world.Database.RunAsOwnerAsync("CREATE FUNCTION public.zqx_taken() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'zqx' USING ERRCODE = '" + errorCode + "'; END $$");
        await world.Database.RunAsOwnerAsync(
            "CREATE TRIGGER zqx_taken BEFORE INSERT ON public.derivation_label FOR EACH ROW WHEN (NEW.label = 'mapa-reversao/1/v2') EXECUTE FUNCTION public.zqx_taken()");
    }

    private sealed class World : IAsyncDisposable
    {
        private World(ScratchDatabase database, NpgsqlDataSource dataSource)
        {
            Database = database;
            DataSource = dataSource;
            Rotation = new DomainLabelRotation(dataSource);
        }

        internal ScratchDatabase Database { get; }

        internal NpgsqlDataSource DataSource { get; }

        internal DomainLabelRotation Rotation { get; }

        /// <summary>A migrated database with two domains, each with the labels of its first generation, as the creation leaves them.</summary>
        internal static async Task<World> CreateAsync()
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var world = new World(database, NpgsqlDataSource.Create(database.OwnerConnectionString));
            var creation = new DomainCreation(world.DataSource);
            var both = DerivationLabels.Of(AllOfBoth);
            _ = await creation.CreateAsync("zqx-domain-a", both, TestContext.Current.CancellationToken);
            _ = await creation.CreateAsync("zqx-domain-b", both, TestContext.Current.CancellationToken);
            return world;
        }

        internal async Task<long> CountAsync(string statement)
        {
            await using var command = DataSource.CreateCommand(statement);
            return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        }

        internal async Task<string> TextAsync(string statement)
        {
            await using var command = DataSource.CreateCommand(statement);
            return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture)!;
        }

        public async ValueTask DisposeAsync()
        {
            await DataSource.DisposeAsync();
            await Database.DisposeAsync();
        }
    }
}
