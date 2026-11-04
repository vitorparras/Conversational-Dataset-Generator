using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Core.Persistence.Content;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The load of a restore by the credential of the application (#64, CA-5): the functions of the step 0027 put back the rows of a copy, the events with the sequence and the digests the copy has,
/// only inside a window that opens once, in a database that never held anything, and closes for good, approved or not. Everything else is refused, and nothing of a refused call stays.
/// </summary>
[Trait("Category", "Database")]
public sealed class RestoreLoadTests(RestoreSource source) : IClassFixture<RestoreSource>
{
    private const string Refused = "CE005";
    private const string Chain =
        "SELECT string_agg(sequence || ':' || encode(previous_digest, 'hex') || ':' || encode(digest, 'hex')"
        + " || ':' || occurred_at || ':' || recorded_by, ',' ORDER BY sequence) FROM public.custody_event";
    private const string People = "SELECT string_agg(id || ':' || name || ':' || created_at || ':' || created_by || ':' || active, ',' ORDER BY id) FROM public.person";
    private const string HeldByDomain =
        "SELECT string_agg(domain_id || ':' || table_name || ':' || live, ',' ORDER BY domain_id, table_name)"
        + " FROM (SELECT domain_id, table_name, sum(delta) AS live FROM public.content_domain GROUP BY domain_id, table_name) c";
    private const string Window ="SELECT string_agg(state || ':' || coalesce(approved::text, '-'), ',' ORDER BY state) FROM public.restore_window";

    [DatabaseFact]
    public async Task EveryTableComesBackAsTheCopyHasItAndTheChainWithTheSameSequenceAndDigests()
    {
        await using var destination = await Destination.CreateAsync();

        await destination.OpenAsync();
        foreach (var table in new[] { "public.domain", "public.derivation_label", "public.person", "public.person_credential", "public.person_change", "public.sign_in_attempt" })
        {
            _ = await destination.LoadAsync(table, source.Copy[table]);
        }

        Assert.Equal(5, await destination.EventsAsync(source.Copy["public.custody_event"]));
        foreach (var table in new[] { "content.item_text", "content.reversal_map", "content.item_emptied" })
        {
            _ = await destination.LoadAsync(table, source.Copy[table]);
        }

        await destination.CloseAsync(approved: true);
        Assert.Equal(await source.World.OwnerColumnAsync(Chain), await destination.Database.FirstColumnAsOwnerAsync(Chain));
        Assert.Equal(await source.World.OwnerColumnAsync(People), await destination.Database.FirstColumnAsOwnerAsync(People));
        Assert.True((await new CustodyVerifier(destination.App, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken)).Passed);
        Assert.Equal(["closed:true,opened:-"], await destination.Database.FirstColumnAsOwnerAsync(Window));
        foreach (var (table, key) in new[]
        {
            ("public.domain", "id"),
            ("public.derivation_label", "label"),
            ("public.person", "id"),
            ("public.person_credential", "person_id"),
            ("public.person_change", "id"),
            ("public.sign_in_attempt", "id"),
        })
        {
            Assert.Equal(source.Copy[table], Assert.Single(await destination.Database.FirstColumnAsOwnerAsync($"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY {key}), '[]') FROM {table} t")));
        }

        Assert.Equal(
            ["1:item_text:1,1:reversal_map:1"],
            await destination.Database.FirstColumnAsOwnerAsync(HeldByDomain));
        Assert.Equal(
            ["2", "2"],
            await destination.Database.FirstColumnAsOwnerAsync(
                "SELECT last_value::text FROM content.item_text_id_seq"
                + " UNION ALL SELECT last_value::text FROM public.person_id_seq"));
        Assert.Equal("1,0", (string?)await destination.InDomainAsync(1, "SELECT count(*) || ',' || (SELECT count(*) FROM content.item_emptied) FROM content.item_text"));
        Assert.Equal(
            "1,1,0",
            (string?)await destination.InDomainAsync(
                2,
                "SELECT (SELECT count(*) FROM content.item_text WHERE emptied_at IS NOT NULL AND raw_text IS NULL)"
                + " || ',' || (SELECT count(*) FROM content.item_emptied)"
                + " || ',' || (SELECT count(*) FROM content.reversal_map)"));
    }

    [DatabaseFact]
    public async Task SignInAttemptsComeBackAsTheCopyHasThemAndTheNextOneContinuesTheSequence()
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();

        var loaded = await destination.LoadAsync("public.sign_in_attempt", source.Copy["public.sign_in_attempt"]);
        await destination.Database.RunAsOwnerAsync("INSERT INTO public.sign_in_attempt (network) VALUES (NULL)");

        Assert.Equal(2, loaded);
        Assert.Equal(
            source.Copy["public.sign_in_attempt"],
            Assert.Single(await destination.Database.FirstColumnAsOwnerAsync("SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY id), '[]') FROM (SELECT * FROM public.sign_in_attempt WHERE id <= 2) t")));
        Assert.Equal(["3"], await destination.Database.FirstColumnAsOwnerAsync("SELECT max(id)::text FROM public.sign_in_attempt"));
    }

    [DatabaseFact]
    public async Task ContentDomainIsCountedByTheTriggersWhileTheContentComesBackAndCountedAgainWhenTheWindowClosesApproved()
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();
        foreach (var table in new[] { "public.domain", "public.derivation_label", "public.person", "public.person_credential", "public.person_change" })
        {
            _ = await destination.LoadAsync(table, source.Copy[table]);
        }

        _ = await destination.EventsAsync(source.Copy["public.custody_event"]);
        foreach (var table in new[] { "content.item_text", "content.reversal_map", "content.item_emptied" })
        {
            _ = await destination.LoadAsync(table, source.Copy[table]);
        }

        var counted = await destination.Database.FirstColumnAsOwnerAsync(HeldByDomain);
        await destination.CloseAsync(approved: true);

        Assert.Equal(["1:item_text:1,1:reversal_map:1,2:item_text:1"], counted);
        Assert.Equal(["1:item_text:1,1:reversal_map:1"], await destination.Database.FirstColumnAsOwnerAsync(HeldByDomain));
        Assert.Equal(["2"], await destination.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.content_domain"));
    }

    [DatabaseTheory]
    [InlineData("INSERT INTO public.domain (name) VALUES ('zqx')")]
    [InlineData("INSERT INTO public.person (name, function_name) VALUES ('zqx', 'Anotação')")]
    [InlineData("INSERT INTO public.derivation_label (label) VALUES ('zqx')")]
    [InlineData("INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 0, 'verify_chain', 'verification_passed')")]
    public async Task WindowDoesNotOpenInADatabaseThatHeldSomething(string held)
    {
        await using var destination = await Destination.CreateAsync();
        await destination.Database.RunAsOwnerAsync(held);

        await destination.RefusedAsync("restore_open");

        Assert.Empty(await destination.Database.FirstColumnAsOwnerAsync("SELECT state FROM public.restore_window"));
    }

    [DatabaseTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WindowThatClosedRefusesEverythingAndKeepsWhatWasLoaded(bool approved)
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();
        _ = await destination.LoadAsync("public.domain", source.Copy["public.domain"]);
        await destination.CloseAsync(approved);

        await destination.RefusedAsync("restore_open");
        await destination.RefusedAsync("restore_close", approved);
        await destination.RefusedAsync("restore_load", "public.domain", "[]");
        await destination.RefusedAsync("restore_events", "[]");

        Assert.Equal(["2"], await destination.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.domain"));
        Assert.Equal([$"closed:{(approved ? "true" : "false")},opened:-"], await destination.Database.FirstColumnAsOwnerAsync(Window));
    }

    [DatabaseFact]
    public async Task WindowOpensOnceAndNothingIsLoadedWithoutIt()
    {
        await using var destination = await Destination.CreateAsync();

        await destination.RefusedAsync("restore_load", "public.domain", source.Copy["public.domain"]);
        await destination.RefusedAsync("restore_events", source.Copy["public.custody_event"]);
        await destination.RefusedAsync("restore_close", true);
        await destination.OpenAsync();
        await destination.RefusedAsync("restore_open");

        Assert.Equal(["opened:-"], await destination.Database.FirstColumnAsOwnerAsync(Window));
        Assert.Equal(["0"], await destination.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.domain"));
    }

    [DatabaseTheory]
    [InlineData("public.custody_event")]
    [InlineData("public.schema_version")]
    [InlineData("public.restore_window")]
    [InlineData("public.content_domain")]
    public async Task TableThatTheLoadDoesNotPutBackIsRefusedAndTheWindowClosesAsFailed(string table)
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();

        Assert.Equal(-1, await destination.LoadAsync(table, "[]"));

        await AssertClosedAsFailedAsync(destination);
    }

    [DatabaseFact]
    public async Task RowThatTheTableRefusesUndoesTheCallAndClosesTheWindowAsFailed()
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();
        Assert.Equal(2, await destination.LoadAsync("public.domain", source.Copy["public.domain"]));

        Assert.Equal(-1, await destination.LoadAsync("public.domain", source.Copy["public.domain"]));

        Assert.Equal(["2"], await destination.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.domain"));
        await AssertClosedAsFailedAsync(destination);
    }

    [DatabaseFact]
    public async Task PersonInactiveInTheCopyIsActiveWhileTheChainComesBackAndInactiveWhenTheWindowClosesApproved()
    {
        const string State = "SELECT string_agg(name || ':' || active, ',' ORDER BY id) FROM public.person";
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();
        _ = await destination.LoadAsync("public.domain", source.Copy["public.domain"]);
        _ = await destination.LoadAsync("public.person", source.Copy["public.person"]);
        Assert.Equal(["zqx-active:true,zqx-gone:true"], await destination.Database.FirstColumnAsOwnerAsync(State));

        await destination.CloseAsync(approved: true);

        Assert.Equal(["zqx-active:true,zqx-gone:false"], await destination.Database.FirstColumnAsOwnerAsync(State));
    }

    [DatabaseFact]
    public async Task TwoSessionsOpeningTheWindowAtOnceLeaveOneOpenedAndTheOtherRefused()
    {
        await using var destination = await Destination.CreateAsync();

        var answers = await Task.WhenAll(destination.OpenOrRefusedAsync(), destination.OpenOrRefusedAsync());

        Assert.Equal([string.Empty, Refused], answers.OrderBy(answer => answer, StringComparer.Ordinal));
        Assert.Equal(["opened:-"], await destination.Database.FirstColumnAsOwnerAsync(Window));
    }

    private static async Task AssertClosedAsFailedAsync(Destination destination)
    {
        Assert.Equal(["closed:false,opened:-"], await destination.Database.FirstColumnAsOwnerAsync(Window));
        await destination.RefusedAsync("restore_open");
        await destination.RefusedAsync("restore_close", true);
        await destination.RefusedAsync("restore_load", "public.domain", "[]");
        await destination.RefusedAsync("restore_events", "[]");
    }

    [DatabaseFact]
    public async Task OnlyTheApplicationRoleExecutesTheFunctionsOfTheLoad()
    {
        await using var destination = await Destination.CreateAsync();
        await using var bare = await destination.Database.CreateBareRoleAsync();

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => bare.RunAsync("SELECT public.restore_open()"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
        Assert.Empty(await destination.Database.FirstColumnAsOwnerAsync("SELECT state FROM public.restore_window"));
    }

    [DatabaseTheory]
    [InlineData("DELETE FROM public.restore_window")]
    [InlineData("UPDATE public.restore_window SET state = 'closed', approved = true")]
    [InlineData("TRUNCATE public.restore_window")]
    public async Task WindowIsChangedByNobodyNotEvenTheOwner(string statement)
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => destination.Database.RunAsOwnerAsync(statement));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
        Assert.Equal(["opened:-"], await destination.Database.FirstColumnAsOwnerAsync(Window));
    }

    [DatabaseTheory]
    [InlineData("INSERT INTO public.restore_window (state) VALUES ('opened')")]
    [InlineData("INSERT INTO public.domain (name) VALUES ('zqx')")]
    public async Task ApplicationCannotOpenTheWindowOrPutRowsBackByItself(string statement)
    {
        await using var destination = await Destination.CreateAsync();
        await using var command = destination.App.CreateCommand(statement);

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
    }

    [DatabaseTheory]
    [InlineData("digest", -3)]
    [InlineData("field", -3)]
    [InlineData("order", -2)]
    [InlineData("first-missing", -2)]
    [InlineData("gap", -4)]
    public async Task EventThatDoesNotComeBackAsTheCopyHasItIsNamedByItsSequenceNothingOfTheCallStaysAndTheWindowClosesAsFailed(string defect, long answer)
    {
        await using var destination = await Destination.CreateAsync();
        await destination.OpenAsync();
        _ = await destination.LoadAsync("public.domain", source.Copy["public.domain"]);
        _ = await destination.LoadAsync("public.person", source.Copy["public.person"]);

        Assert.Equal(answer, await destination.EventsAsync(Spoil(source.Copy["public.custody_event"], defect)));

        await AssertClosedAsFailedAsync(destination);
        Assert.Equal(["0"], await destination.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.custody_event"));
    }

    private static string Spoil(string events, string defect)
    {
        var rows = JsonNode.Parse(events)!.AsArray();
        switch (defect)
        {
            case "digest":
                rows[2]!["digest"] = "\\x" + new string('0', 64);
                break;
            case "field":
                rows[2]!["item_id"] = 9;
                break;
            case "order":
                (rows[0], rows[1]) = (rows[1]!.DeepClone(), rows[0]!.DeepClone());
                break;
            case "first-missing":
                rows.RemoveAt(0);
                break;
            default:
                rows.RemoveAt(2);
                break;
        }

        return rows.ToJsonString();
    }

    private sealed class Destination : IAsyncDisposable
    {
        private Destination(ScratchDatabase database) => (Database, App) = (database, NpgsqlDataSource.Create(database.AppConnectionString));

        internal ScratchDatabase Database { get; }

        internal NpgsqlDataSource App { get; }

        internal static async Task<Destination> CreateAsync()
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            return new Destination(database);
        }

        internal Task OpenAsync() => CallAsync("restore_open");

        internal Task CloseAsync(bool approved) => CallAsync("restore_close", approved);

        internal async Task<string> OpenOrRefusedAsync()
        {
            try
            {
                _ = await CallAsync("restore_open");
                return string.Empty;
            }
            catch (PostgresException refusal)
            {
                return refusal.SqlState;
            }
        }

        internal async Task<long> LoadAsync(string table, string rows) => (long)(await CallAsync("restore_load", table, rows))!;

        internal async Task<long> EventsAsync(string rows) => (long)(await CallAsync("restore_events", rows))!;

        internal async Task<object?> InDomainAsync(int domain, string statement)
        {
            await using var transaction = await ContentTransaction.BeginAsync(App, domain, TestContext.Current.CancellationToken);
            await using var command = transaction.CreateCommand(statement);
            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        internal async Task RefusedAsync(string function, params object[] arguments)
        {
            var refusal = await Assert.ThrowsAsync<PostgresException>(() => CallAsync(function, arguments));
            Assert.Equal(Refused, refusal.SqlState);
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            await Database.DisposeAsync();
        }

        [SuppressMessage("Security", "CA2100", Justification = "The function is a literal of the calling tests and the arguments are parameters.")]
        private async Task<object?> CallAsync(string function, params object[] arguments)
        {
            // nosemgrep: sql-concatenation-csharp interpolates only the name of a restore_* function, a literal of the calling tests; the arguments are parameters.
            await using var command = App.CreateCommand($"SELECT public.{function}({string.Join(", ", arguments.Select((_, index) => $"@p{index}"))})");
            for (var index = 0; index < arguments.Length; index++)
            {
                var parameter = new NpgsqlParameter($"p{index}", arguments[index]);
                parameter.NpgsqlDbType = arguments[index] is string text && text.StartsWith('[') ? NpgsqlDbType.Jsonb : parameter.NpgsqlDbType;
                _ = command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return result is DBNull ? null : result;
        }
    }
}
