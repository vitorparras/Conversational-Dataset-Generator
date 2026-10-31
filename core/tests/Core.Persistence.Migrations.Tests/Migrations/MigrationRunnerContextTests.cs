using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Migrations;

/// <summary>A library call never resumes on the caller's context: the runner is awaited by hosts that have one, and must not wait for it.</summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerContextTests
{
    [DatabaseFact]
    public async Task RunOnAnEmptyPoolNeverResumesOnTheCallersContext()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.OwnerConnectionString);
        var context = new CountingContext();

        var result = await context.StartAsync(() => new MigrationRunner(dataSource, EmbeddedMigrations.Load()).RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal(MigrationOutcome.Applied, result.Outcome);
        Assert.Equal(0, context.Posts);
    }

    [DatabaseFact]
    public async Task RunOverAPooledSessionNeverResumesOnTheCallersContext()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var pooled = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString) { Pooling = true }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(pooled);
        await using (var warm = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal(System.Data.ConnectionState.Open, warm.State);
        }

        var context = new CountingContext();

        var result = await context.StartAsync(() => new MigrationRunner(dataSource, EmbeddedMigrations.Load()).RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal(MigrationOutcome.Applied, result.Outcome);
        Assert.Equal(0, context.Posts);
    }
}
