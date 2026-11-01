using System.Globalization;
using System.Text.RegularExpressions;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// What the triggers of the server hold of an event once the privileges are no longer in the way (#59, CA-1 to CA-3): whoever is given UPDATE, DELETE or TRUNCATE back, the application's role
/// or the owner's, in an ordinary session or in one that skips ordinary triggers, meets the SQLSTATE of the refusal, the chain is left as it was, and the server's own log says what was
/// refused without a value of the event. The refusal by privilege that comes first is the subject of <see cref="CustodyChainGuaranteeTests"/>.
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
        var kept = (await database.EventsAsync()).Single(row => row.Sequence == sequence);
        await database.RunAsOwnerAsync($"GRANT {Privileges} ON public.custody_event TO app");
        var tag = $"zqx-{Guid.NewGuid():N}";

        var refused = await RefusedAsync(await database.AppConnectionStringAsync(), operation, sequence, tag);
        var block = await LoggedRefusalAsync(tag);

        AssertRefusal(refused, operation, sequence);
        var logged = string.Join('\n', block);
        Assert.Contains($"ERROR:  public.custody_event: {operation} refused", logged, StringComparison.Ordinal);
        Assert.Equal(operation != "TRUNCATE", logged.Contains($"for the event of sequence {sequence}", StringComparison.Ordinal));
        foreach (var value in ValuesOf(kept))
        {
            Assert.DoesNotContain(value, logged, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>The lines the server wrote for the refusal of the statement tagged, from its ERROR to the STATEMENT that repeats it, of one session; the log of the container can lag.</summary>
    private static async Task<IReadOnlyList<string>> LoggedRefusalAsync(string tag)
    {
        var shared = await PostgresFixture.SharedAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var lines = (await shared.ServerLogAsync()).Split('\n');
            var statement = Array.FindIndex(lines, line => line.Contains("STATEMENT:", StringComparison.Ordinal) && line.Contains(tag, StringComparison.Ordinal));
            if (statement >= 0)
            {
                var session = Regex.Match(lines[statement], @"\[\d+\] ").Value;
                var error = Array.FindLastIndex(lines, statement, line => line.Contains(session, StringComparison.Ordinal) && line.Contains("ERROR:", StringComparison.Ordinal));
                Assert.True(error >= 0, "The STATEMENT of the refusal has no ERROR of the same session before it.");
                return lines[error..(statement + 1)];
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, $"The server log has no refused statement tagged {tag} after 30 seconds.");
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
