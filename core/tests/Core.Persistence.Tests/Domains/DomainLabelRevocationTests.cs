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
/// The revocation of a label of a domain (issue 56, part 1b), over a migrated database of its own and the credential of the owner: the revocation and the event of the chain exist together
/// or not at all, the label stays registered, the generation left to encrypt under is never revoked, and no role changes or removes a revocation (CA-4).
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainLabelRevocationTests
{
    private const string Labels = "SELECT count(*) FROM public.derivation_label";
    private const string Revocations = "SELECT count(*) FROM public.derivation_label_revocation";
    private const string Events = "SELECT count(*) FROM public.custody_event";
    private const string RevokedEvents = "SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_revoked'";

    private static readonly string[] AllOfBoth =
    [
        "texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1",
        "texto-bruto/1/v2", "mapa-reversao/1/v2", "texto-bruto/1/v3", "mapa-reversao/1/v3",
    ];

    [DatabaseFact]
    public async Task Ca4ALabelRevokedAfterARotationIsRecordedWithItsEventAndStaysRegistered()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();

        var result = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.Revoked, 1, 1), (result.Outcome, result.DomainId, result.Version));
        Assert.Equal("texto-bruto/1/v1", Assert.Single(await LabelRevocations.ReadAsync(world.DataSource, TestContext.Current.CancellationToken)));
        Assert.Equal((6L, 1L), (await world.CountAsync(Labels), await world.CountAsync(Revocations)));
    }

    [DatabaseFact]
    public async Task Ca4TheEventOfTheRevocationNamesTheDomainAndTheLabelBySystemAndTheChainVerifies()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();

        var result = await world.Revocation.RevokeAsync("mapa-reversao/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal(
            "system,domain,1,domain_label_revoked,administrative_act,mapa-reversao/1/v1",
            await world.TextAsync(
                "SELECT concat_ws(',', CASE WHEN actor_system THEN 'system' END, item_kind, item_id, step, reason, reference) FROM public.custody_event WHERE step = 'domain_label_revoked'"));
        Assert.Equal((long)result.Sequence!, await world.CountAsync("SELECT sequence FROM public.custody_event WHERE step = 'domain_label_revoked'"));
        Assert.True((await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken)).Passed);
    }

    [DatabaseFact]
    public async Task Ca4TheOnlyGenerationLeftIsRefusedAndNothingIsWritten()
    {
        await using var world = await World.CreateAsync();
        var before = (await world.CountAsync(Revocations), await world.CountAsync(Events));

        var result = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal(DomainLabelOutcome.LastGeneration, result.Outcome);
        Assert.Equal(before, (await world.CountAsync(Revocations), await world.CountAsync(Events)));
    }

    [DatabaseFact]
    public async Task Ca4TheNewestGenerationIsRefusedUntilARotationAddsAnother()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        var newest = await world.Revocation.RevokeAsync("texto-bruto/1/v2", TestContext.Current.CancellationToken);
        await world.RotateAsync();

        var afterwards = await world.Revocation.RevokeAsync("texto-bruto/1/v2", TestContext.Current.CancellationToken);
        var keptByRevoked = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.LastGeneration, DomainLabelOutcome.Revoked, DomainLabelOutcome.Revoked), (newest.Outcome, afterwards.Outcome, keptByRevoked.Outcome));
    }

    [DatabaseFact]
    public async Task Ca4ARevokedNewerGenerationDoesNotKeepAnOlderOneRevocable()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        await world.Database.RunAsOwnerAsync("INSERT INTO public.derivation_label_revocation (label) VALUES ('texto-bruto/1/v2')");

        var result = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal(DomainLabelOutcome.LastGeneration, result.Outcome);
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

        var result = await world.Revocation.RevokeAsync(label, TestContext.Current.CancellationToken);

        Assert.Equal((expected, 0L, 0L), (result.Outcome, await world.CountAsync(Revocations), await world.CountAsync(RevokedEvents)));
    }

    [DatabaseFact]
    public async Task Ca4ALabelRevokedTwiceIsRefusedTheSecondTimeWithNoSecondEvent()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        _ = await world.Revocation.RevokeAsync("mapa-reversao/1/v1", TestContext.Current.CancellationToken);

        var again = await world.Revocation.RevokeAsync("mapa-reversao/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.AlreadyRevoked, 1L, 1L), (again.Outcome, await world.CountAsync(RevokedEvents), await world.CountAsync(Revocations)));
    }

    [DatabaseFact]
    public async Task Ca4RevokingALabelOfOneDomainTouchesNeitherTheOtherDomainNorTheOtherPurpose()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();

        _ = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal("texto-bruto/1/v1", Assert.Single(await LabelRevocations.ReadAsync(world.DataSource, TestContext.Current.CancellationToken)));
        Assert.Equal(1L, await world.CountAsync("SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_revoked' AND item_id = 1 AND reference NOT LIKE 'mapa-reversao/%'"));
    }

    [DatabaseFact]
    public async Task Ca4AChainThatRefusesTheEventLeavesNoRevocationAndTheRefusalIsTheChains()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        var failed = CustodyEvent.Verification(eventsRead: 1, firstInconsistent: 1);
        Assert.Equal(CustodyOutcome.Appended, (await new CustodyChain(world.DataSource).AppendAsync(failed, TestContext.Current.CancellationToken)).Outcome);

        var result = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.ChainRefused, 0L), (result.Outcome, await world.CountAsync(Revocations)));
    }

    [DatabaseFact]
    public async Task Ca4AWriterThatMetAnotherAtTheSameLinkOfTheChainIsAConflictAndLeavesNoRevocation()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        var meeting = new DomainLabelRevocation(world.DataSource, (_, _, _) => Task.FromResult(new CustodyResult { Outcome = CustodyOutcome.Conflict }));

        var result = await meeting.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.Conflict, 0L), (result.Outcome, await world.CountAsync(Revocations)));
    }

    [DatabaseFact]
    public async Task Ca4ARevocationThatAnotherWroteFirstIsAConflictAndNothingOfThisOneIsKept()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        await world.Database.RunAsOwnerAsync("CREATE FUNCTION public.zqx_taken() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'zqx' USING ERRCODE = 'unique_violation'; END $$");
        await world.Database.RunAsOwnerAsync("CREATE TRIGGER zqx_taken BEFORE INSERT ON public.derivation_label_revocation FOR EACH ROW EXECUTE FUNCTION public.zqx_taken()");

        var result = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

        Assert.Equal((DomainLabelOutcome.Conflict, 0L, 0L), (result.Outcome, await world.CountAsync(Revocations), await world.CountAsync(RevokedEvents)));
    }

    [DatabaseFact]
    public async Task Ca4NoRoleChangesOrRemovesARevocationAndTheApplicationCannotWriteOne()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        _ = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);

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
    public async Task Ca4TheApplicationReadsTheRevocationsAndOnlyARegisteredLabelCanBeRevoked()
    {
        await using var world = await World.CreateAsync();
        await world.RotateAsync();
        _ = await world.Revocation.RevokeAsync("texto-bruto/1/v1", TestContext.Current.CancellationToken);
        await using var application = NpgsqlDataSource.Create(world.Database.AppConnectionString);

        var seen = await LabelRevocations.ReadAsync(application, TestContext.Current.CancellationToken);
        var unregistered = await World.RefusedAsync(world.Database.OwnerConnectionString, "INSERT INTO public.derivation_label_revocation (label) VALUES ('texto-bruto/9/v1')");

        Assert.Equal((true, PostgresErrorCodes.ForeignKeyViolation), (seen.Contains("texto-bruto/1/v1"), unregistered.SqlState));
    }

    [Fact]
    public async Task TheRevocationNeedsItsDataSourceItsWayOfAddingAndTheTextOfTheLabel()
    {
        await using var source = NpgsqlDataSource.Create("Host=zqx-never-connected");
        var revocation = new DomainLabelRevocation(source);

        Assert.Equal(
            ["dataSource", "dataSource", "append", "label"],
            [
                Assert.Throws<ArgumentNullException>(() => new DomainLabelRevocation(null!)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new DomainLabelRevocation(null!, (_, _, _) => throw new TimeoutException())).ParamName,
                Assert.Throws<ArgumentNullException>(() => new DomainLabelRevocation(source, null!)).ParamName,
                (await Assert.ThrowsAsync<ArgumentNullException>(() => revocation.RevokeAsync(null!, TestContext.Current.CancellationToken))).ParamName,
            ]);
    }

    [Fact]
    public async Task TheRegisterOfRevocationsIsReadOnlyFromADataSource() =>
        Assert.Equal("dataSource", (await Assert.ThrowsAsync<ArgumentNullException>(() => LabelRevocations.ReadAsync(null!, TestContext.Current.CancellationToken))).ParamName);

    [Theory]
    [InlineData(false, false, false, DomainLabelOutcome.LabelUnknown)]
    [InlineData(true, true, false, DomainLabelOutcome.AlreadyRevoked)]
    [InlineData(true, false, false, DomainLabelOutcome.LastGeneration)]
    [InlineData(true, true, true, DomainLabelOutcome.AlreadyRevoked)]
    [InlineData(true, false, true, null)]
    public void WhatIsKnownOfALabelDecidesWhetherItsRevocationGoesOn(bool known, bool revoked, bool newerKept, DomainLabelOutcome? expected)
    {
        var refusal = new LabelState(known, revoked, newerKept).Refusal();

        Assert.Equal(expected, refusal);
    }

    private sealed class World : IAsyncDisposable
    {
        private World(ScratchDatabase database, NpgsqlDataSource dataSource)
        {
            Database = database;
            DataSource = dataSource;
            Revocation = new DomainLabelRevocation(dataSource);
        }

        internal ScratchDatabase Database { get; }

        internal NpgsqlDataSource DataSource { get; }

        internal DomainLabelRevocation Revocation { get; }

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

        [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
        internal static async Task<PostgresException> RefusedAsync(string connectionString, string statement)
        {
            await using var connection = await Sql.OpenAsync(connectionString);
            await using var command = new NpgsqlCommand(statement, connection);
            return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        /// <summary>Passes the domain 1 to its next generation, which the custody of the test holds.</summary>
        internal async Task RotateAsync() =>
            Assert.Equal(DomainLabelOutcome.Rotated, (await new DomainLabelRotation(DataSource).RotateAsync(1, DerivationLabels.Of(AllOfBoth), TestContext.Current.CancellationToken)).Outcome);

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
