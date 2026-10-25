using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Migrations;

/// <summary>
/// The five guarantees of the server still pass, as the application's role, once the migration has run: the registry table belongs
/// to owner, gives app a read and nothing more, and has no row policy.
/// </summary>
[Trait("Category", "Database")]
public sealed class MigratedDatabaseGuaranteesTests
{
    [DatabaseFact]
    public async Task ApplicationRoleHoldsNoExcessPrivilegeOnTheMigratedDatabase()
    {
        var result = await RunOnTheMigratedDatabaseAsync(dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task TablesOfTheMigratedDatabaseNeedNoForcedRowSecurity()
    {
        var result = await RunOnTheMigratedDatabaseAsync(dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task ServerOpensASerializableTransactionOnTheMigratedDatabase()
    {
        var result = await RunOnTheMigratedDatabaseAsync(dataSource => new SerializableCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task PgauditStaysLoadedAndInstalledOnTheMigratedDatabase()
    {
        var result = await RunOnTheMigratedDatabaseAsync(dataSource => new PgAuditCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task ApplicationRoleHoldsNoColumnGrantVoidedByATableGrantOnTheMigratedDatabase()
    {
        var result = await RunOnTheMigratedDatabaseAsync(dataSource => new ColumnRevokeCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task StartOnTheMigratedDatabaseIsReadyAfterEveryCheck()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(steps);

        var report = await AppStartup.RunAsync(database, steps);

        Assert.True(report.Ready, report.FirstFailure?.Message);
        Assert.Equal(["Privilege", "RlsForce", "Serializable", "PgAudit", "ColumnRevoke", "SchemaVersion"], report.Results.Select(result => result.Name));
    }

    private static async Task<CheckResult> RunOnTheMigratedDatabaseAsync(Func<NpgsqlDataSource, IStartupCheck> check)
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return await AppStartup.CheckAsync(database, check);
    }
}
