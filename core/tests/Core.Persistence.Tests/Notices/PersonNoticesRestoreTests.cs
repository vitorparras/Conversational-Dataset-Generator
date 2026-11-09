using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.Notices;

/// <summary>
/// The events of a copy come back with the verdicts they had (issue 595, CA-3): while the window of the restoration is open a verdict that failed in the past tells nobody that the writing
/// is suspended, not even the people who were active when the copy was made.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonNoticesRestoreTests
{
    private const string AllPeople = "SELECT jsonb_agg(to_jsonb(p) ORDER BY id)::text FROM public.person p";
    private const string AllEvents = "SELECT jsonb_agg(to_jsonb(e) ORDER BY sequence)::text FROM public.custody_event e";
    private const string LoadPeople = "SELECT public.restore_load('public.person', @rows)::text";
    private const string LoadEvents = "SELECT public.restore_events(@rows)::text";

    [DatabaseFact]
    public async Task AFailedVerdictOfTheCopyComingBackTellsNobody()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = await MigratedAsync();
        await using var sourceData = NpgsqlDataSource.Create(source.AppConnectionString);
        _ = await new PersonRegistry(sourceData).CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, token);
        _ = await new CustodyChain(sourceData).AppendAsync(CustodyEvent.Verification(1, 1), token);
        await using var target = await MigratedAsync();

        var (people, events) = (await TextAsync(source.OwnerConnectionString, AllPeople), await TextAsync(source.OwnerConnectionString, AllEvents));
        _ = await TextAsync(target.AppConnectionString, "SELECT public.restore_open()::text");
        var loaded = (await LoadedAsync(target, LoadPeople, people), await LoadedAsync(target, LoadEvents, events));

        Assert.Equal(("1", "2"), loaded);
        Assert.Equal("0", await TextAsync(target.OwnerConnectionString, "SELECT count(*) FROM public.person_notice"));
    }

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is a literal of this class, never built from input.")]
    private static async Task<string> TextAsync(string connectionString, string statement)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is a constant of this class, never built from input.")]
    private static async Task<string> LoadedAsync(ScratchDatabase database, string statement, string rows)
    {
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        _ = command.Parameters.AddWithValue("rows", NpgsqlDbType.Jsonb, rows);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }
}
