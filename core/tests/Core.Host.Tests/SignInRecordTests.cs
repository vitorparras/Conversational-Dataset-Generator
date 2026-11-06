using System.Net;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>A refusal of the entry that does not reach the chain leaves a line in the log with how the chain answered and the prefix of the origin, and nothing the person typed.</summary>
[Trait("Category", "Database")]
public sealed class SignInRecordTests
{
    private const string Tries = "public.zqx_tries";

    [DatabaseFact]
    public async Task RefusalThatConflictsInAllThreeTriesLeavesALineWithTheOutcomeAndThePrefix()
    {
        await using var database = await ConflictingDatabaseAsync();
        using var recorded = new RecordedLog();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(recorded));
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var record = new SignInRecord(new SignInAttempts(source), factory);

        await record.RefusedAsync(author: null, SignInRefusal.AccountNotRecognized, IPAddress.Parse("203.0.113.77"), TestContext.Current.CancellationToken);

        var line = Assert.Single(recorded.Lines);
        Assert.StartsWith("Warning: ", line, StringComparison.Ordinal);
        Assert.Contains("(Conflict)", line, StringComparison.Ordinal);
        Assert.Contains("prefixo 203.0.113.0/24", line, StringComparison.Ordinal);
        Assert.Equal(["3"], await database.FirstColumnAsOwnerAsync($"SELECT last_value::text FROM {Tries}"));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.sign_in_attempt"));
    }

    [DatabaseFact]
    public async Task RefusalThatTheChainTookLeavesNoLineAndAnUnknownOriginHasNoPrefix()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        using var recorded = new RecordedLog();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(recorded));
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);

        await new SignInRecord(new SignInAttempts(source), factory)
            .RefusedAsync(author: null, SignInRefusal.AccountNotRecognized, origin: null, TestContext.Current.CancellationToken);

        Assert.Empty(recorded.Lines);
        Assert.Equal(["1"], await database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.sign_in_attempt"));
    }

    /// <summary>A migrated database whose table of attempts refuses every insert with a serialization failure, as a writer that always meets another would, counting the tries.</summary>
    private static async Task<ScratchDatabase> ConflictingDatabaseAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            $"CREATE SEQUENCE {Tries}",
            $"GRANT USAGE ON SEQUENCE {Tries} TO app",
            $"CREATE FUNCTION public.zqx_conflict() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM nextval('{Tries}'); RAISE EXCEPTION 'zqx' USING ERRCODE = '40001'; END $$",
            "CREATE TRIGGER zqx_conflict BEFORE INSERT ON public.sign_in_attempt FOR EACH ROW EXECUTE FUNCTION public.zqx_conflict()");
        return database;
    }
}
