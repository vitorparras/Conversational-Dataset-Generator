using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The start is refused while the window of the load of a restore is open, because whoever was loading died before closing it (#64, 4d); a window that was closed, approved or not, does not refuse.</summary>
public sealed class RestoreWindowCheckTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, true)]
    public void OnlyAWindowOpenedAndNeverClosedIsMissing(long opened, long closed, bool holds)
    {
        Assert.Equal(holds ? CheckStatus.Passed : CheckStatus.Failed, RestoreWindowCheck.Judge(opened, closed).Status);
    }

    [Fact]
    public void RefusalSaysTheRestoreDidNotConcludeAndWhatToDo()
    {
        var message = RestoreWindowCheck.Judge(1, 0).Message;

        Assert.Contains("janela de carga", message, StringComparison.Ordinal);
        Assert.Contains("a restauração não concluiu", message, StringComparison.Ordinal);
        Assert.Contains("descarte este banco", message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MigratedDatabaseThatNeverLoadedAnythingStartsNormally()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());

        var result = await AppStartup.CheckAsync(database, dataSource => new RestoreWindowCheck(dataSource));

        Assert.Equal((CheckStatus.Passed, "RestoreWindow"), (result.Status, result.Name));
    }

    [DatabaseFact]
    public async Task DatabaseBeforeTheStepThatCreatesTheWindowRefusesTheStartNamingTheMigration()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load().Where(step => step.Version < 19).ToList());

        var result = await AppStartup.CheckAsync(database, dataSource => new RestoreWindowCheck(dataSource));

        Assert.Equal((CheckStatus.Failed, "RestoreWindow"), (result.Status, result.Name));
        Assert.Contains("aplique as migrações do núcleo com o migrate", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseWhoseLoadDiedWithTheWindowOpenRefusesTheStartNamingTheWindow()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await RunAsAppAsync(database, "SELECT public.restore_open()");

        var result = await AppStartup.CheckAsync(database, dataSource => new RestoreWindowCheck(dataSource));

        Assert.Equal((CheckStatus.Failed, "RestoreWindow"), (result.Status, result.Name));
        Assert.Contains("janela de carga de uma restauração está aberta", result.Message, StringComparison.Ordinal);
        Assert.Contains("a restauração não concluiu", result.Message, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WindowThatTheLoadClosedApprovedOrNotDoesNotRefuseTheStart(bool approved)
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await RunAsAppAsync(database, "SELECT public.restore_open()");
        await RunAsAppAsync(database, $"SELECT public.restore_close({(approved ? "true" : "false")})");

        var result = await AppStartup.CheckAsync(database, dataSource => new RestoreWindowCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    private static async Task RunAsAppAsync(ScratchDatabase database, string statement)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        await using var command = dataSource.CreateCommand(statement);
        _ = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
