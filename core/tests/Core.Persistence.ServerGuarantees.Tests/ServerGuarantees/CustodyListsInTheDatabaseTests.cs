using System.Text.RegularExpressions;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// What the database accepts after the last migration, against what the code knows (#737). A migration that redefines a list check or the load of a restore from an old definition, without what the
/// ones in between added, undoes them in the database while every test in C# stays green; here the lists the code declares are read back from the database that all the migrations built.
/// </summary>
[Trait("Category", "Database")]
public sealed partial class CustodyListsInTheDatabaseTests
{
    private const string Refused = "CE005";
    private const string NoRows = "[]";
    private const string NotLoadedByTheLoad = "public.custody_event";
    private const string NotInTheCopy = "public.schema_version";

    [DatabaseFact]
    public async Task EveryStepTheCodeNamesIsInTheStepCheckAndNoOtherIs() =>
        await AssertListedAsync("custody_event_step_listed", Enum.GetValues<CustodyStep>().Select(CustodyNames.Of));

    [DatabaseFact]
    public async Task EveryKindOfItemTheCodeNamesIsInTheKindCheckAndNoOtherIs() =>
        await AssertListedAsync("custody_event_item_kind_listed", Enum.GetValues<CustodyItemKind>().Select(CustodyNames.Of));

    [DatabaseFact]
    public async Task EveryReasonTheCodeNamesIsInTheReasonCheckAndNoOtherIs() =>
        await AssertListedAsync("custody_event_reason_listed", Enum.GetValues<CustodyReason>().Select(CustodyNames.Of));

    [DatabaseFact]
    public async Task TheLoadOfARestoreAcceptsEveryTableOfTheCopyWithNoRows()
    {
        await using var database = await MigratedAsync();
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        _ = await CallAsync(app, "restore_open");

        foreach (var table in CopyTables.All.Select(copied => copied.Name).Where(name => name != NotLoadedByTheLoad))
        {
            var loaded = (long)(await CallAsync(app, "restore_load", table, NoRows))!;

            Assert.True(loaded == 0, $"restore_load recusou a tabela {table} da cópia (devolveu {loaded}).");
        }

        Assert.Equal(0L, (long)(await CallAsync(app, "restore_events", NoRows))!);
    }

    [DatabaseFact]
    public async Task TheLoadOfARestoreRefusesATableOutsideTheCopy()
    {
        await using var database = await MigratedAsync();
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        _ = await CallAsync(app, "restore_open");

        var loaded = (long)(await CallAsync(app, "restore_load", NotInTheCopy, NoRows))!;

        Assert.Equal(-1L, loaded);
        var again = await Assert.ThrowsAsync<PostgresException>(() => CallAsync(app, "restore_load", "public.person", NoRows));
        Assert.Equal(Refused, again.SqlState);
    }

    [GeneratedRegex("'([^']+)'", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Literal();

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    private static async Task AssertListedAsync(string constraint, IEnumerable<string> declared)
    {
        await using var database = await MigratedAsync();

        var definition = Assert.Single(await database.FirstColumnAsOwnerAsync(
            $"SELECT pg_catalog.pg_get_constraintdef(oid) FROM pg_catalog.pg_constraint WHERE conrelid = 'public.custody_event'::regclass AND conname = '{constraint}'"));

        var listed = Literal().Matches(definition).Select(match => match.Groups[1].Value).ToArray();
        var missing = declared.Except(listed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var extra = listed.Except(declared, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0 && extra.Length == 0, $"{constraint}: faltam no banco [{string.Join(", ", missing)}]; sobram no banco [{string.Join(", ", extra)}].");
    }

    private static async Task<object?> CallAsync(NpgsqlDataSource app, string function, params object[] arguments)
    {
        // nosemgrep: sql-concatenation-csharp interpolates only the name of a restore_* function, a literal of the calling tests; the arguments are parameters.
        await using var command = app.CreateCommand($"SELECT public.{function}({string.Join(", ", arguments.Select((_, index) => $"@p{index}"))})");
        for (var index = 0; index < arguments.Length; index++)
        {
            var parameter = new NpgsqlParameter($"p{index}", arguments[index]);
            parameter.NpgsqlDbType = arguments[index] is string text && text.StartsWith('[') ? NpgsqlTypes.NpgsqlDbType.Jsonb : parameter.NpgsqlDbType;
            _ = command.Parameters.Add(parameter);
        }

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }
}
