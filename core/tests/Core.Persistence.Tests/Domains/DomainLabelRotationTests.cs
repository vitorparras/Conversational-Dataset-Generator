using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Domains;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Domains;

/// <summary>
/// The rotation and the revocation of the label of a domain (issue 56, part 1), over a migrated database of its own and the credential of the owner: the labels of the next generation or the
/// revocation and the event of the chain exist together or not at all, another domain is never touched, and no label is changed or removed (CA-1 to CA-6).
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainLabelRotationTests
{
    private const string Labels = "SELECT count(*) FROM public.derivation_label";
    private const string Revocations = "SELECT count(*) FROM public.derivation_label_revocation";
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
    public async Task Ca4ALabelRevokedAfterARotationIsRecordedWithItsEventAndTheLabelStaysRegistered()
    {
        await using var world = await World.CreateAsync();
        _ = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);

        var result = await world.Rotation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.Revoked, 1, 1), (result.Outcome, result.DomainId, result.Version));
        var revoked = await LabelRevocations.ReadAsync(world.DataSource, TestContext.Current.CancellationToken);
        Assert.Equal("texto-bruto/1/v1", Assert.Single(revoked));
        Assert.Equal(6L, await world.CountAsync(Labels));
        Assert.Equal(
            "texto-bruto/1/v1",
            await world.TextAsync("SELECT reference FROM public.custody_event WHERE step = 'domain_label_revoked' AND item_kind = 'domain' AND item_id = 1 AND actor_system"));
    }

    [DatabaseFact]
    public async Task Ca4TheNewestGenerationNotRevokedIsNotRevokedAndNothingIsWritten()
    {
        await using var world = await World.CreateAsync();
        var before = (await world.CountAsync(Revocations), await world.CountAsync(Events));

        var result = await world.Rotation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal(DomainLabelOutcome.LastGeneration, result.Outcome);
        Assert.Equal(before, (await world.CountAsync(Revocations), await world.CountAsync(Events)));
    }

    [DatabaseFact]
    public async Task Ca4RevokingTheNewestAfterTheOlderIsStillRefusedUntilARotationAddsAnother()
    {
        await using var world = await World.CreateAsync();
        var custody = DerivationLabels.Of(AllOfBoth);
        _ = await world.Rotation.RotateAsync(1, custody, TestContext.Current.CancellationToken);

        var newest = await world.Rotation.RevokeAsync("texto-bruto/1/v2", TestContext.Current.CancellationToken);
        _ = await world.Rotation.RotateAsync(1, custody, TestContext.Current.CancellationToken);
        var afterwards = await world.Rotation.RevokeAsync("texto-bruto/1/v2", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.LastGeneration, DomainLabelOutcome.Revoked), (newest.Outcome, afterwards.Outcome));
    }

    [DatabaseTheory]
    [InlineData("texto-bruto/1/v7", DomainLabelOutcome.LabelUnknown)]
    [InlineData("texto-bruto/5/v1", DomainLabelOutcome.LabelUnknown)]
    [InlineData("nome-do-domínio", DomainLabelOutcome.LabelInvalid)]
    [InlineData("texto-bruto/01/v1", DomainLabelOutcome.LabelInvalid)]
    [InlineData("", DomainLabelOutcome.LabelInvalid)]
    public async Task Ca4ALabelThatIsNotRegisteredOrNotALabelIsRefusedAndNothingIsWritten(string label, DomainLabelOutcome expected)
    {
        await using var world = await World.CreateAsync();

        var result = await world.Rotation.RevokeAsync(label, TestContext.Current.CancellationToken);

        Assert.Equal((expected, 0L), (result.Outcome, await world.CountAsync(Revocations)));
    }

    [DatabaseFact]
    public async Task Ca4ALabelRevokedTwiceIsRefusedTheSecondTimeWithNoSecondEvent()
    {
        await using var world = await World.CreateAsync();
        _ = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);
        _ = await world.Rotation.RevokeAsync("mapa-reversao/1/v1", TestContext.Current.CancellationToken);

        var again = await world.Rotation.RevokeAsync("mapa-reversao/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.AlreadyRevoked, 1L), (again.Outcome, await world.CountAsync("SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_revoked'")));
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
    public async Task Ca6NoRoleChangesOrRemovesARevocationAndTheApplicationCannotWriteOne()
    {
        await using var world = await World.CreateAsync();
        _ = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);
        _ = await world.Rotation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        var update = await World.RefusedAsync(world.Database.OwnerConnectionString, "UPDATE public.derivation_label_revocation SET revoked_by = 'x'");
        var delete = await World.RefusedAsync(world.Database.OwnerConnectionString, "DELETE FROM public.derivation_label_revocation");
        var truncate = await World.RefusedAsync(world.Database.OwnerConnectionString, "TRUNCATE public.derivation_label_revocation");
        var insert = await World.RefusedAsync(world.Database.AppConnectionString, "INSERT INTO public.derivation_label_revocation (label) VALUES ('texto-bruto/2/v1')");

        Assert.Equal(
            (PostgresErrorCodes.RestrictViolation, PostgresErrorCodes.RestrictViolation, PostgresErrorCodes.RestrictViolation, PostgresErrorCodes.InsufficientPrivilege),
            (update.SqlState, delete.SqlState, truncate.SqlState, insert.SqlState));
        Assert.Equal(1L, await world.CountAsync(Revocations));
    }

    [DatabaseFact]
    public async Task Ca6TheApplicationReadsTheRevocationsAndALabelRevokedMustBeARegisteredOne()
    {
        await using var world = await World.CreateAsync();
        _ = await world.Rotation.RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken);
        _ = await world.Rotation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);
        await using var application = NpgsqlDataSource.Create(world.Database.AppConnectionString);

        var seen = await LabelRevocations.ReadAsync(application, TestContext.Current.CancellationToken);
        var unregistered = await World.RefusedAsync(world.Database.OwnerConnectionString, "INSERT INTO public.derivation_label_revocation (label) VALUES ('texto-bruto/9/v1')");

        Assert.Equal((true, PostgresErrorCodes.ForeignKeyViolation), (seen.Contains("texto-bruto/1/v1"), unregistered.SqlState));
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

        [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
        internal static async Task<PostgresException> RefusedAsync(string connectionString, string statement)
        {
            await using var connection = await Sql.OpenAsync(connectionString);
            await using var command = new NpgsqlCommand(statement, connection);
            return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
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
