using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>What a normal start does against the database, as the application's role: the server guarantees and then the version of the schema.</summary>
internal static class AppStartup
{
    /// <summary>The checks in the order the composition root registers them, over the data source built with the application's credential.</summary>
    public static IReadOnlyList<IStartupCheck> Checks(NpgsqlDataSource dataSource, IReadOnlyList<MigrationStep> steps) =>
    [
        new PrivilegeCheck(dataSource),
        new RlsForceCheck(dataSource),
        new SerializableCheck(dataSource),
        new PgAuditCheck(dataSource),
        new ColumnRevokeCheck(dataSource),
        new SchemaVersionCheck(dataSource, steps),
        new ProtectedColumnCheck(dataSource),
    ];

    /// <summary>The report of a start against the database, as the application's role, with the steps this build is meant to carry.</summary>
    public static async Task<StartupReport> RunAsync(ScratchDatabase database, IReadOnlyList<MigrationStep> steps)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        return await new StartupRunner(Checks(dataSource, steps), TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The result of one check, as the application's role.</summary>
    public static async Task<CheckResult> CheckAsync(ScratchDatabase database, Func<NpgsqlDataSource, IStartupCheck> check)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        return await check(dataSource).RunAsync(TestContext.Current.CancellationToken);
    }
}
