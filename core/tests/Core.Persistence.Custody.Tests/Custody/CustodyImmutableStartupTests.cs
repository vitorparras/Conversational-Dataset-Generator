using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// What a start against the server does about the guarantees of the custody event (#59, CA-4 and CA-5): the application's role owning the table is accused, and a trigger that was disabled,
/// brought back in a mode that skips some sessions, removed or put back narrower than the migration made it is accused, the start is refused, and the chain, whole all the time,
/// shows that the refusal is not about a broken link. Each test has a database of its own, because each one tampers with it.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyImmutableStartupTests
{
    private const string Changed = "custody_event_never_changed";
    private const string Emptied = "custody_event_never_emptied";
    private const string Table = "public.custody_event";
    private const string Wrong = "não dispara em toda sessão";
    private const string Disabled = "está desabilitado";
    private const string Absent = "não está em public.custody_event como a migração 0009 o declara";

    private const string Allowing =
        "CREATE FUNCTION public.zqx_allows() RETURNS trigger LANGUAGE plpgsql AS $allow$ BEGIN RETURN OLD; END $allow$";

    [DatabaseFact]
    public async Task DatabaseAsTheMigrationLeftItStartsWithTheChainWhole()
    {
        await using var database = new CustodyDatabase();
        _ = await SeedAsync(database);

        var report = await StartAsync(database);

        Assert.True(report.Ready, report.FirstFailure?.Message);
        Assert.Equal(CheckStatus.Passed, report.Results.Single(result => result.Name == "Immutability").Status);
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    [DatabaseTheory]
    [InlineData("disable the trigger of the row", $"ALTER TABLE {Table} DISABLE TRIGGER {Changed}", Changed, Disabled)]
    [InlineData("disable the trigger of the statement", $"ALTER TABLE {Table} DISABLE TRIGGER {Emptied}", Emptied, Disabled)]
    [InlineData("enable the trigger of the row in the ordinary mode", $"ALTER TABLE {Table} ENABLE TRIGGER {Changed}", Changed, Wrong)]
    [InlineData("enable the trigger of the statement in the ordinary mode", $"ALTER TABLE {Table} ENABLE TRIGGER {Emptied}", Emptied, Wrong)]
    [InlineData("enable the trigger of the row for replicas only", $"ALTER TABLE {Table} ENABLE REPLICA TRIGGER {Changed}", Changed, Wrong)]
    [InlineData("enable the trigger of the statement for replicas only", $"ALTER TABLE {Table} ENABLE REPLICA TRIGGER {Emptied}", Emptied, Wrong)]
    [InlineData("remove the trigger of the row", $"DROP TRIGGER {Changed} ON {Table}", Changed, Absent)]
    [InlineData("remove the trigger of the statement", $"DROP TRIGGER {Emptied} ON {Table}", Emptied, Absent)]
    [InlineData(
        "put the trigger of the row back for UPDATE alone",
        $"DROP TRIGGER {Changed} ON {Table} | CREATE TRIGGER {Changed} BEFORE UPDATE ON {Table} FOR EACH ROW EXECUTE FUNCTION public.refuse_custody_event_change() | "
        + $"ALTER TABLE {Table} ENABLE ALWAYS TRIGGER {Changed}",
        Changed,
        Absent)]
    [InlineData(
        "put the trigger of the row back to fire after the change",
        $"DROP TRIGGER {Changed} ON {Table} | CREATE TRIGGER {Changed} AFTER UPDATE OR DELETE ON {Table} FOR EACH ROW EXECUTE FUNCTION public.refuse_custody_event_change() | "
        + $"ALTER TABLE {Table} ENABLE ALWAYS TRIGGER {Changed}",
        Changed,
        Absent)]
    [InlineData(
        "put the trigger of the statement back for each row",
        $"DROP TRIGGER {Emptied} ON {Table} | CREATE TRIGGER {Emptied} BEFORE UPDATE ON {Table} FOR EACH ROW EXECUTE FUNCTION public.refuse_custody_event_emptying() | "
        + $"ALTER TABLE {Table} ENABLE ALWAYS TRIGGER {Emptied}",
        Emptied,
        Absent)]
    [InlineData(
        "put the trigger of the row back with a function that allows",
        $"{Allowing} | DROP TRIGGER {Changed} ON {Table} | CREATE TRIGGER {Changed} BEFORE UPDATE OR DELETE ON {Table} FOR EACH ROW EXECUTE FUNCTION public.zqx_allows() | "
        + $"ALTER TABLE {Table} ENABLE ALWAYS TRIGGER {Changed}",
        Changed,
        Absent)]
    public async Task TamperedTriggerIsAccusedAndTheStartIsRefusedWithTheChainWhole(string tampering, string statements, string trigger, string wording)
    {
        await using var database = new CustodyDatabase();
        _ = await SeedAsync(database);
        ArgumentNullException.ThrowIfNull(statements);
        foreach (var statement in statements.Split(" | "))
        {
            await database.RunAsOwnerAsync(statement);
        }

        var report = await StartAsync(database);

        var failure = Assert.Single(report.Results, result => result.Status == CheckStatus.Failed);
        Assert.False(report.Ready, tampering);
        Assert.Equal("Immutability", failure.Name);
        Assert.Contains($"o gatilho {trigger} ", failure.Message, StringComparison.Ordinal);
        Assert.Contains(wording, failure.Message, StringComparison.Ordinal);
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    [DatabaseFact]
    public async Task BothTriggersGoneAreBothAccusedAndTheStartIsRefused()
    {
        await using var database = new CustodyDatabase();
        _ = await SeedAsync(database);
        await database.RunAsOwnerAsync($"DROP TRIGGER {Changed} ON {Table}");
        await database.RunAsOwnerAsync($"DROP TRIGGER {Emptied} ON {Table}");

        var report = await StartAsync(database);

        var failure = Assert.Single(report.Results, result => result.Status == CheckStatus.Failed);
        Assert.Equal("Immutability", failure.Name);
        Assert.Contains($"o gatilho {Changed} ", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"o gatilho {Emptied} ", failure.Message, StringComparison.Ordinal);
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    [DatabaseFact]
    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of this test with the sequence it appended, never input.")]
    public async Task TriggerBroughtBackInTheOrdinaryModeLetsAnOwnerSessionThatSkipsOrdinaryTriggersThrough()
    {
        await using var database = new CustodyDatabase();
        var sequence = await SeedAsync(database);
        await database.RunAsOwnerAsync($"ALTER TABLE {Table} ENABLE TRIGGER {Changed}");
        await database.RunAsOwnerAsync($"GRANT UPDATE ON {Table} TO owner");
        await using var session = await ReplicaSession.OfAsync(database, "owner");
        await using var connection = await Sql.OpenAsync(session.ConnectionString);
        await using var command = new Npgsql.NpgsqlCommand($"UPDATE {Table} SET step = step WHERE sequence = {sequence}", connection);

        var changed = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var report = await StartAsync(database);

        Assert.Equal(1, changed);
        Assert.Equal(CheckStatus.Failed, report.Results.Single(result => result.Name == "Immutability").Status);
    }

    [DatabaseFact]
    public async Task ApplicationRoleThatOwnsTheEventTableIsAccusedAndTheStartIsRefused()
    {
        await using var database = new CustodyDatabase();
        _ = await SeedAsync(database);
        await database.RunAsSuperuserAsync($"ALTER TABLE {Table} OWNER TO app");

        var report = await StartAsync(database);

        var failure = Assert.Single(report.Results, result => result.Status == CheckStatus.Failed);
        Assert.False(report.Ready);
        Assert.Equal("Privilege", failure.Name);
        Assert.Matches(@"'app' responde como dono da tabela (public\.)?custody_event,", failure.Message);
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    /// <summary>The checks of a start against this database as the application reads it, in the order of the composition root, with the immutability after the version of the schema.</summary>
    private static async Task<StartupReport> StartAsync(CustodyDatabase database)
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(await database.AppConnectionStringAsync());
        IReadOnlyList<IStartupCheck> checks = [.. AppStartup.Checks(dataSource, EmbeddedMigrations.Load()), new ImmutabilityCheck(dataSource)];
        return await new StartupRunner(checks, TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Three events in the chain, written by the application's role; the sequence of the last.</summary>
    private static async Task<long> SeedAsync(CustodyDatabase database)
    {
        var chain = await database.ChainAsync();
        var last = 0L;
        foreach (var item in new long[] { 7_403_918, 7_403_919, 7_403_920 })
        {
            var custodyEvent = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
            last = (await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken)).Sequence!.Value;
        }

        return last;
    }
}
