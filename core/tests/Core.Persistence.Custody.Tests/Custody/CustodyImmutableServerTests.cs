using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// What the triggers of the server hold of an event once the privileges are no longer in the way (#59, CA-1 to CA-3): whoever is given UPDATE, DELETE or TRUNCATE back, the application's role
/// or the owner's, in an ordinary session or in one that skips ordinary triggers, meets the SQLSTATE of the refusal, the chain is left as it was, and the server's own log has the audit line of what was
/// attempted without a value of the event. The refusal by privilege that comes first is the subject of <see cref="CustodyChainGuaranteeTests"/>.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyImmutableServerTests : IClassFixture<CustodyDatabase>
{
    private const string Refused = "CE001";
    private const long Item = 7_403_918;
    private const string Privileges = "UPDATE, DELETE, TRUNCATE";

    private readonly CustodyDatabase database;

    public CustodyImmutableServerTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public async Task ApplicationRoleGivenThePrivilegeBackIsRefusedByTheTrigger(string operation)
    {
        var sequence = await AppendOneAsync();
        var before = await ChainAsync();
        await database.RunAsOwnerAsync($"GRANT {Privileges} ON public.custody_event TO app");

        var refused = await RefusedAsync(await database.AppConnectionStringAsync(), operation, sequence);

        Assert.Equal(before, await ChainAsync());
        AssertRefusal(refused, operation, sequence);
    }

    [DatabaseTheory]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public async Task OwnerRoleWithThePrivilegeIsRefusedByTheTrigger(string operation)
    {
        var sequence = await AppendOneAsync();
        var before = await ChainAsync();
        await database.RunAsOwnerAsync($"GRANT {Privileges} ON public.custody_event TO owner");

        var refused = await RefusedAsync(await database.OwnerConnectionStringAsync(), operation, sequence);

        Assert.Equal(before, await ChainAsync());
        AssertRefusal(refused, operation, sequence);
    }

    [DatabaseTheory]
    [InlineData("app", "UPDATE")]
    [InlineData("app", "DELETE")]
    [InlineData("app", "TRUNCATE")]
    [InlineData("owner", "UPDATE")]
    [InlineData("owner", "DELETE")]
    [InlineData("owner", "TRUNCATE")]
    public async Task SessionThatSkipsOrdinaryTriggersIsRefusedForTheCredential(string credential, string operation)
    {
        var sequence = await AppendOneAsync();
        var before = await ChainAsync();
        await database.RunAsOwnerAsync($"GRANT {Privileges} ON public.custody_event TO {credential}");
        await using var session = await ReplicaSession.OfAsync(database, credential);

        var mode = await ReplicaSession.ModeOfAsync(session.ConnectionString);
        var refused = await RefusedAsync(session.ConnectionString, operation, sequence);

        Assert.Equal("replica", mode);
        Assert.Equal(before, await ChainAsync());
        AssertRefusal(refused, operation, sequence);
    }

    [DatabaseTheory]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public async Task SuperuserInASessionThatSkipsOrdinaryTriggersIsRefusedByTheTrigger(string operation)
    {
        var sequence = await AppendOneAsync();
        var before = await ChainAsync();
        var connection = new NpgsqlConnectionStringBuilder(await database.SuperuserConnectionStringAsync()) { Options = "-c session_replication_role=replica" }.ConnectionString;

        var mode = await ReplicaSession.ModeOfAsync(connection);
        var refused = await RefusedAsync(connection, operation, sequence);

        Assert.Equal("replica", mode);
        Assert.Equal(before, await ChainAsync());
        AssertRefusal(refused, operation, sequence);
    }

    [DatabaseTheory]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public async Task RefusedAttemptIsInTheServerLogWithTheOperationAndTheSequenceAndNoValueOfTheEvent(string operation)
    {
        var sequence = await AppendOneAsync();
        var before = await ChainAsync();
        var kept = (await database.EventsAsync()).Single(row => row.Sequence == sequence);
        await database.RunAsOwnerAsync($"GRANT {Privileges} ON public.custody_event TO app");
        var tag = $"zqx-{Guid.NewGuid():N}";

        var refused = await RefusedAsync(await database.AppConnectionStringAsync(), operation, sequence, tag);
        var line = await LoggedAttemptAsync(tag);

        AssertRefusal(refused, operation, sequence);
        Assert.Equal(before, await ChainAsync());
        Assert.Contains(",\"app\",", line, StringComparison.Ordinal);
        Assert.Contains($",WRITE,{operation}", line, StringComparison.Ordinal);
        Assert.Contains("public.custody_event", line, StringComparison.Ordinal);
        Assert.Equal(operation != "TRUNCATE", line.Contains($"sequence = {sequence}", StringComparison.Ordinal));
        var statement = line[line.IndexOf("AUDIT: SESSION,", StringComparison.Ordinal)..];
        foreach (var value in ValuesOf(kept))
        {
            Assert.DoesNotContain(value, statement, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> ValuesOf(EventRow row) =>
    [
        Item.ToString(CultureInfo.InvariantCulture),
        row.Step,
        row.Reason,
        Convert.ToHexString(row.Digest),
        Convert.ToHexString(row.PreviousDigest),
    ];

    private static void AssertRefusal(PostgresException refused, string operation, long sequence)
    {
        Assert.Equal(Refused, refused.SqlState);
        Assert.Contains($"public.custody_event: {operation} refused", refused.MessageText, StringComparison.Ordinal);
        Assert.Equal(operation != "TRUNCATE", refused.MessageText.Contains($"sequence {sequence}", StringComparison.Ordinal));
        Assert.Equal(("public", "custody_event"), (refused.SchemaName, refused.TableName));
    }

    private static string Statement(string operation, long sequence, string tag) => operation switch
    {
        "UPDATE" => $"/* {tag} */ UPDATE public.custody_event SET step = step WHERE sequence = {sequence}",
        "DELETE" => $"/* {tag} */ DELETE FROM public.custody_event WHERE sequence = {sequence}",
        _ => $"/* {tag} */ TRUNCATE public.custody_event",
    };

    private static Task<PostgresException> RefusedAsync(string connectionString, string operation, long sequence, string tag = "zqx") =>
        CustodyDatabase.RefusedOnAsync(connectionString, Statement(operation, sequence, tag));

    /// <summary>
    /// The line of the server log that audits the statement tagged: the server logs a statement before its triggers run, so the attempt is
    /// there although the refusal is not (the log keeps no error text, issue #60); the log can lag the statement.
    /// </summary>
    private static async Task<string> LoggedAttemptAsync(string tag)
    {
        var shared = await PostgresFixture.SharedAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var line = (await shared.ServerLogAsync()).Split('\n')
                .FirstOrDefault(candidate => candidate.Contains("AUDIT: SESSION,", StringComparison.Ordinal) && candidate.Contains(tag, StringComparison.Ordinal));
            if (line is not null)
            {
                return line;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, $"The server log has no audit line for the statement tagged {tag} after 30 seconds.");
            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The chain as it is, in a form that compares: the sequence, every field and both digests.</summary>
    private async Task<IReadOnlyList<string>> ChainAsync() =>
        [.. (await database.EventsAsync()).Select(Compared)];

    private static string Compared(EventRow row) =>
        string.Join(
            '|',
            row.Sequence,
            row.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
            row.ItemId,
            row.Step,
            row.Reason,
            Convert.ToHexString(row.PreviousDigest),
            Convert.ToHexString(row.Digest));

    private async Task<long> AppendOneAsync()
    {
        var chain = await database.ChainAsync();
        var custodyEvent = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, Item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        var appended = await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken);
        return appended.Sequence!.Value;
    }
}
