using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.Migrations;

/// <summary>
/// The step 0032 (issue 647): the load of a restore takes the table of the revocations of the labels, which the 0027 refused, and changes nothing else about who may run it or what it refuses.
/// </summary>
[Trait("Category", "Database")]
public sealed class RestoreLoadRevocationStepTests
{
    private const string Step = "0032-restore-load-revocation";
    private const string Labels = "public.derivation_label";
    private const string Revocations = "public.derivation_label_revocation";
    private const string Label = /*lang=json,strict*/ """[{"label": "zqx-label/1/v1", "first_used_at": "2026-01-02T03:04:05+00", "first_used_by": "zqx-writer"}]""";
    private const string Revoked = /*lang=json,strict*/ """[{"label": "zqx-label/1/v1", "revoked_at": "2026-02-03T04:05:06+00", "revoked_by": "zqx-owner"}]""";
    private const string Orphan = /*lang=json,strict*/ """[{"label": "zqx-label/1/v9", "revoked_at": "2026-02-03T04:05:06+00", "revoked_by": "zqx-owner"}]""";
    private const string Window = "SELECT string_agg(state || ':' || coalesce(approved::text, '-'), ',' ORDER BY state) FROM public.restore_window";
    private const string Held = """SELECT label || '|' || to_char(revoked_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS') || '|' || revoked_by FROM public.derivation_label_revocation""";

    private static int Adding => EmbeddedMigrations.Load().Single(step => step.Name == Step).Version;

    [DatabaseFact]
    public async Task BeforeTheStepTheLoadRefusesTheRevocationsAndClosesTheWindowAsFailed()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync([.. EmbeddedMigrations.Load().Where(step => step.Version < Adding)]);
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        _ = await CallAsync(app, "SELECT public.restore_open()");
        Assert.Equal(1L, await LoadAsync(app, Labels, Label));

        var answer = await LoadAsync(app, Revocations, Revoked);

        Assert.Equal(-1L, answer);
        Assert.Equal(["closed:false,opened:-"], await database.FirstColumnAsOwnerAsync(Window));
    }

    [DatabaseFact]
    public async Task AfterTheStepTheLoadPutsTheRevocationBackAsTheCopyHasIt()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        _ = await CallAsync(app, "SELECT public.restore_open()");
        Assert.Equal(1L, await LoadAsync(app, Labels, Label));

        var answer = await LoadAsync(app, Revocations, Revoked);

        Assert.Equal(1L, answer);
        Assert.Equal(["zqx-label/1/v1|2026-02-03T04:05:06|zqx-owner"], await database.FirstColumnAsOwnerAsync(Held));
        Assert.Equal(["opened:-"], await database.FirstColumnAsOwnerAsync(Window));
    }

    [DatabaseFact]
    public async Task ARevocationOfALabelTheCopyDoesNotHoldIsRefusedNothingStaysAndTheWindowClosesAsFailed()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        _ = await CallAsync(app, "SELECT public.restore_open()");
        Assert.Equal(1L, await LoadAsync(app, Labels, Label));

        var answer = await LoadAsync(app, Revocations, Orphan);

        Assert.Equal(-1L, answer);
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.derivation_label_revocation"));
        Assert.Equal(["closed:false,opened:-"], await database.FirstColumnAsOwnerAsync(Window));
    }

    [DatabaseFact]
    public async Task AfterTheStepOnlyTheApplicationExecutesTheLoadAndATableOutsideTheListIsStillRefused()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var bare = await database.CreateBareRoleAsync();
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        _ = await CallAsync(app, "SELECT public.restore_open()");

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => bare.RunAsync("SELECT public.restore_open()"));
        var outside = await LoadAsync(app, "public.schema_version", "[]");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
        Assert.Equal(-1L, outside);
        Assert.Equal(["closed:false,opened:-"], await database.FirstColumnAsOwnerAsync(Window));
    }

    private static async Task<long> LoadAsync(NpgsqlDataSource app, string table, string rows) =>
        (long)(await CallAsync(app, "SELECT public.restore_load(@table, @rows)", new NpgsqlParameter("table", table), new NpgsqlParameter("rows", NpgsqlDbType.Jsonb) { Value = rows }))!;

    private static async Task<object?> CallAsync(NpgsqlDataSource app, string statement, params NpgsqlParameter[] parameters)
    {
        await using var command = app.CreateCommand(statement);
        command.Parameters.AddRange(parameters);
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }
}
