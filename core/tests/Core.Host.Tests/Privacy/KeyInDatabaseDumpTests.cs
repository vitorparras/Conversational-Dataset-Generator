using System.Text;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Support.Custody;

namespace Core.Host.Tests.Privacy;

/// <summary>After the host has started with a key of the custody files over a migrated database, no spelling of that key is in what pg_dump writes for the database.</summary>
[Trait("Category", "Database")]
public sealed class KeyInDatabaseDumpTests
{
    private static readonly byte[] Key = CustodyFiles.SyntheticKey();

    [DatabaseFact]
    public async Task DatabaseDumpHoldsNoSpellingOfTheKeyTheHostStartedWith()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        var status = await KeyedHost.RunAsync(database.AppConnectionString, cancellationToken);

        var dump = await database.DumpAsync();

        Assert.Equal("Passed", KeyedHost.StatusOf(status, "MasterKey"));
        Assert.Equal("Passed", KeyedHost.StatusOf(status, "SchemaVersion"));
        Assert.Contains("INSERT INTO public.schema_version", dump, StringComparison.Ordinal);
        Assert.False(KeySpellings.HoldsAny(Encoding.UTF8.GetBytes(dump), Key));
    }
}
