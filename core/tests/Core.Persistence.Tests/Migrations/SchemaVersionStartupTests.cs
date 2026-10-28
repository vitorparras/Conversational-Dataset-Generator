using System.Globalization;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Migrations;

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

    [DatabaseFact]
    public async Task MigratedDatabaseConfirmsTheLastVersionOfTheEmbeddedMigrations()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(steps);

        var confirmed = await ConfirmedAsync(database, steps);

        Assert.Equal(VersionValue.Present(steps[^1].Version.ToString(CultureInfo.InvariantCulture)), confirmed);
    }

    [DatabaseFact]
    public async Task CheckThatDidNotRunYetConfirmsNoVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(steps);
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);

        var check = new SchemaVersionCheck(dataSource, steps);

        Assert.Equal(VersionValue.Absent, check.ConfirmedVersion);
    }

    [DatabaseFact]
    public async Task EmptyDatabaseConfirmsNoVersionNotEvenZero()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        var confirmed = await ConfirmedAsync(database, EmbeddedMigrations.Load());

        Assert.Equal(VersionValue.Absent, confirmed);
    }

    [DatabaseFact]
    public async Task DatabaseBehindTheBuildConfirmsNoVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var applied = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(applied);

        var confirmed = await ConfirmedAsync(database, TestSteps.Following(applied, Table));

        Assert.Equal(VersionValue.Absent, confirmed);
    }

    [DatabaseFact]
    public async Task DatabaseAheadOfTheBuildConfirmsNoVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var built = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(TestSteps.Following(built, Table));

        var confirmed = await ConfirmedAsync(database, built);

        Assert.Equal(VersionValue.Absent, confirmed);
    }

    [DatabaseFact]
    public async Task DatabaseWithAStepOfOtherContentConfirmsNoVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(Table));

        var confirmed = await ConfirmedAsync(database, TestSteps.OnTopOfTheEmbedded(Altered));

        Assert.Equal(VersionValue.Absent, confirmed);
    }

    [DatabaseFact]
    public async Task RunThatFailsAfterOneThatPassedWithdrawsTheVersion()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(steps);
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        var check = new SchemaVersionCheck(dataSource, steps);
        _ = await check.RunAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(VersionValue.Absent, check.ConfirmedVersion);
        await database.RevokeRegistryReadAsync();

        _ = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, check.ConfirmedVersion);
    }

    private static async Task<VersionValue> ConfirmedAsync(ScratchDatabase database, IReadOnlyList<MigrationStep> steps)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        var check = new SchemaVersionCheck(dataSource, steps);
        _ = await check.RunAsync(TestContext.Current.CancellationToken);
        return check.ConfirmedVersion;
    }

    private static Task<CheckResult> CheckAsync(ScratchDatabase database, IReadOnlyList<MigrationStep> steps) =>
        AppStartup.CheckAsync(database, dataSource => new SchemaVersionCheck(dataSource, steps));
}
