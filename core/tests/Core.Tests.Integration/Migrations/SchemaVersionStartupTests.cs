using Core.Persistence.Migrations;
using Core.Platform.Startup;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Migrations;

/// <summary>A normal start with the application's credential against a database behind, ahead or diverging from the build (CA-2): it refuses and repairs nothing.</summary>
[Trait("Category", "Database")]
public sealed class SchemaVersionStartupTests
{
    private static readonly (string Name, string Sql) Table = ("creates-table", "CREATE TABLE zqx_table (a integer)");
    private static readonly (string Name, string Sql) Altered = ("creates-table", "CREATE TABLE zqx_table (a integer, b integer)");

    [DatabaseFact]
    public async Task EmptyDatabaseIsRefusedAsVersionZeroNamingTheVersionTheBuildExpects()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();

        var result = await CheckAsync(database, steps);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("SchemaVersion", result.Name);
        Assert.Contains($"esquema na versão 0, esta instalação espera a {steps[^1].Version}", result.Message, StringComparison.Ordinal);
        Assert.Contains("modo migrate", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseBehindTheBuildIsRefusedNamingTheFoundAndTheExpectedVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var applied = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(applied);
        var steps = TestSteps.Following(applied, Table);

        var result = await CheckAsync(database, steps);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains($"esquema na versão {applied[^1].Version}, esta instalação espera a {steps[^1].Version}", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseAheadOfTheBuildIsRefusedNamingTheFoundAndTheExpectedVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var built = EmbeddedMigrations.Load();
        var ahead = TestSteps.Following(built, Table);
        _ = await database.MigrateAsync(ahead);

        var result = await CheckAsync(database, built);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains($"esquema na versão {ahead[^1].Version}, à frente da {built[^1].Version}", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseWithAStepOfOtherContentIsRefusedNamingTheStepAndBothDigests()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var applied = TestSteps.OnTopOfTheEmbedded(Table);
        _ = await database.MigrateAsync(applied);
        var altered = TestSteps.OnTopOfTheEmbedded(Altered);

        var result = await CheckAsync(database, altered);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains($"o passo {altered[^1].Name} foi aplicado com conteúdo diferente", result.Message, StringComparison.Ordinal);
        Assert.Contains($"registrado {applied[^1].Sha256}, atual {altered[^1].Sha256}", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MigratedDatabasePassesNamingTheVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(steps);

        var result = await CheckAsync(database, steps);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Contains($"esquema na versão {steps[^1].Version}, a que esta instalação espera", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task StartAgainstAnEmptyDatabaseStopsAtTheVersionCheckAfterTheGuaranteesPassed()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        var report = await AppStartup.RunAsync(database, EmbeddedMigrations.Load());

        Assert.False(report.Ready);
        Assert.Equal("SchemaVersion", report.FirstFailure!.Name);
        Assert.Equal(
            ["Privilege", "RlsForce", "Serializable", "PgAudit", "ColumnRevoke"],
            report.Results.Where(result => result.Status == CheckStatus.Passed).Select(result => result.Name));
    }

    [DatabaseFact]
    public async Task RefusedStartDoesNotMigrateAndNeverShowsThePassword()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var password = new NpgsqlConnectionStringBuilder(database.AppConnectionString).Password!;

        var report = await AppStartup.RunAsync(database, EmbeddedMigrations.Load());

        Assert.False(await database.TableExistsAsync("schema_version"));
        Assert.DoesNotContain(password, string.Join('|', report.Results), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RegistryTheApplicationRoleCannotReadIsRefusedNamingTheRegistryTable()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(steps);
        await database.RevokeRegistryReadAsync();

        var result = await CheckAsync(database, steps);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("SQLSTATE 42501", result.Message, StringComparison.Ordinal);
        Assert.Contains("public.schema_version", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("catálogo", result.Message, StringComparison.Ordinal);
    }

    private static Task<CheckResult> CheckAsync(ScratchDatabase database, IReadOnlyList<MigrationStep> steps) =>
        AppStartup.CheckAsync(database, dataSource => new SchemaVersionCheck(dataSource, steps));
}
