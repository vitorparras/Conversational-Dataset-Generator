using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.ServerGuarantees.Restore;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A copy of the data put back into a database of its own by the <see cref="RestoreLoader" />, the path the restore mode takes, with the tip noted outside the copy and the window closed approved.
/// </summary>
internal sealed class RestoredCopy : IAsyncDisposable
{
    private RestoredCopy(ScratchDatabase database, NpgsqlDataSource app)
    {
        Database = database;
        App = app;
    }

    public ScratchDatabase Database { get; }

    /// <summary>The data source of the application's role over the restored database.</summary>
    public NpgsqlDataSource App { get; }

    /// <summary>Loads the copy of the world into a new database; a load the loader does not conclude as restored fails the test that asked for it.</summary>
    public static async Task<RestoredCopy> LoadAsync(CopiedWorld copied)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        var restored = new RestoredCopy(database, NpgsqlDataSource.Create(database.AppConnectionString));
        try
        {
            var tip = new RestoreTip(copied.Result.Seal!.Events, copied.Result.Seal.Digest);
            var loader = new RestoreLoader(restored.App, new GuaranteeVerifier(restored.App), TimeProvider.System);
            var result = await loader.LoadAsync(RestoreCopy.Open(copied.Folder), tip, TestContext.Current.CancellationToken);
            Assert.Equal((RestoreOutcome.Restored, string.Empty), (result.Outcome, result.Detail));
            return restored;
        }
        catch
        {
            await restored.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await Database.DisposeAsync();
    }
}
