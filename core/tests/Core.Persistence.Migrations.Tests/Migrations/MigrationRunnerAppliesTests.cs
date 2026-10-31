using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Migrations;

/// <summary>The runner against an empty database and again at the current version (CA-1, CA-5), as the role owner.</summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerAppliesTests
{
    private static readonly (string Name, string Sql)[] TwoTables =
    [
        ("creates-first", "CREATE TABLE zqx_first (a integer)"),
        ("creates-second", "CREATE TABLE zqx_second (a integer)"),
    ];

    [DatabaseFact]
    public async Task EmptyDatabaseReachesTheLastVersionOfTheBuild()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();

        var result = await database.MigrateAsync(steps);

        Assert.Equal(MigrationOutcome.Applied, result.Outcome);
        Assert.Equal(steps[^1].Version, result.Found);
        Assert.Equal(steps[^1].Version, result.Expected);
        Assert.Equal(steps.Select(step => step.Name), result.Applied);
    }

    [DatabaseFact]
    public async Task RegistryHoldsOneRowPerStepInOrderWithItsDigest()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(TwoTables);

        _ = await database.MigrateAsync(steps);

        var rows = await database.ReadRegistryAsync();
        Assert.Equal(steps.Select(step => (step.Version, step.Name, step.Sha256)), rows.Select(row => (row.Version, row.Name, row.Sha256)));
    }

    [DatabaseFact]
    public async Task RegistryRecordsWhenAndByWhomEachStepWasApplied()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        _ = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(TwoTables));

        var rows = await database.ReadRegistryAsync();
        Assert.All(rows, row => Assert.Equal("owner", row.AppliedBy));
        Assert.All(rows, row => Assert.InRange(row.AppliedAt, before, DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(rows.OrderBy(row => row.AppliedAt), rows);
    }

    [DatabaseFact]
    public async Task EachStepOfTheSetTakesEffectInTheSchema()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        _ = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(TwoTables));

        Assert.True(await database.TableExistsAsync("zqx_first"));
        Assert.True(await database.TableExistsAsync("zqx_second"));
    }

    [DatabaseFact]
    public async Task OwnerPasswordReachesNeitherTheRegistryNorTheResult()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var password = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString).Password!;

        var result = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(TwoTables));

        var rows = await database.ReadRegistryAsync();
        Assert.DoesNotContain(password, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, string.Join('|', rows), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ApplicationRoleReadsTheRegistryAfterTheMigration()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(TwoTables);
        _ = await database.MigrateAsync(steps);
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT max(version) FROM public.schema_version", connection);

        var version = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(steps[^1].Version, version);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotWriteTheRegistry()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("INSERT INTO public.schema_version (version, name, sha256) VALUES (99, 'forged', 'x')", connection);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task RerunAtTheCurrentVersionDoesNothing()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(TwoTables);
        _ = await database.MigrateAsync(steps);

        var result = await database.MigrateAsync(steps);

        Assert.Equal(MigrationOutcome.NothingToDo, result.Outcome);
        Assert.Equal(steps[^1].Version, result.Found);
        Assert.Equal(steps[^1].Version, result.Expected);
        Assert.Empty(result.Applied);
    }

    [DatabaseFact]
    public async Task RerunAtTheCurrentVersionLeavesTheRegistryAsItWas()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(TwoTables);
        _ = await database.MigrateAsync(steps);
        var registered = await database.ReadRegistryAsync();

        _ = await database.MigrateAsync(steps);

        Assert.Equal(registered, await database.ReadRegistryAsync());
    }
}
