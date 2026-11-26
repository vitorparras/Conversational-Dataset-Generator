using System.Globalization;
using System.Text;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;
using static Core.Persistence.Tests.Reversal.ReversalAsks;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// The storage of the request to reverse a marker and of its authorization (issues 103 and 104, fatia C): an incomplete request writes nothing, the authorization is another person's of the
/// curation, until a moment to come and one at a time, a renewal is a new row, nothing is written with the chain broken, and a copy of the database does not read the request.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalRequestsTests
{
    private const string ReversalRefusal = "CE007";

    private const string Authorizations =
        "SELECT string_agg(a.authorized_by || '|' || (a.authorized_at <= now()) || '|' || to_char(a.valid_until AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI') || '|' || e.reference, ';' ORDER BY a.id) "
        + "FROM content.reversal_authorization a JOIN public.custody_event e ON e.item_kind = 'reversal_authorization' AND e.item_id = a.id AND e.step = 'reversal_authorized' "
        + "AND e.actor_person_id = a.authorized_by";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task IncompleteRequestWritesNoRowAndNoEvent()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (item, marker) = await ItemAsync(world, 1);
        var events = await CountAsync(world, CountEvents);

        var asked = await world.ReversalRequests.AskAsync(1, 3, Draft(item, marker) with { Term = "0", Reason = " ", Use = null }, Token);

        Assert.Equal((ReversalOutcome.Incomplete, null), (asked.Outcome, asked.Id));
        Assert.Equal([ReversalRequestField.Term, ReversalRequestField.Reason, ReversalRequestField.Use], asked.Missing);
        Assert.Equal((0L, events), (await CountAsync(world, CountRequests), await CountAsync(world, CountEvents)));
    }

    [DatabaseFact]
    public async Task OwnAuthorizationIsRefusedByTheDatabase()
    {
        await using var world = await ContentWorld.CreateAsync();
        var request = await ReversalRows.RequestAsync(world, 1, 3);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => ReversalRows.AuthorizeAsync(world, (1, request), 3, "30 days"));

        Assert.Equal((ReversalRefusal, 0L), (refused.SqlState, await CountAsync(world, CountAuthorizations)));
        _ = await ReversalRows.AuthorizeAsync(world, (1, request), 1, "30 days");
        Assert.Equal(1L, await CountAsync(world, CountAuthorizations));
    }

    [DatabaseFact]
    public async Task RenewalIsANewAuthorizationWithWhoWhenAndTerm()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var request = await AskedAsync(world);
        await ReversalRows.ExpiredAsync(world, (1, request), second);
        Assert.Equal(ReversalState.Expired, (await world.ReversalRequestReader.ReadAsync(1, request, Token))!.State);
        var until = DateTimeOffset.UtcNow.AddDays(7);

        var renewed = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), until, Token);
        var again = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), until.AddDays(1), Token);

        Assert.Equal((ReversalOutcome.Recorded, ReversalOutcome.Closed), (renewed.Outcome, again.Outcome));
        var read = await world.ReversalRequestReader.ReadAsync(1, request, Token);
        Assert.Equal((ReversalState.Authorized, until.ToUnixTimeMilliseconds(), second), (read!.State, read.ValidUntil?.ToUnixTimeMilliseconds(), read.AuthorizedBy));
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToString("u", CultureInfo.InvariantCulture)[..10];
        var rows = ((string)(await world.OwnerInDomainAsync(1, Authorizations))!).Split(';');
        Assert.Equal(2, rows.Length);
        Assert.StartsWith($"{second}|true|{past}", rows[0], StringComparison.Ordinal);
        Assert.Equal($"{second}|true|{until.UtcDateTime.ToString("u", CultureInfo.InvariantCulture)[..16]}|{request}", rows[1]);
    }

    [DatabaseFact]
    public async Task BrokenChainRefusesNewAuthorizations()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var request = await AskedAsync(world);
        var (item, marker) = await ItemAsync(world, 1);
        await world.BreakTheChainAsync();
        var events = await CountAsync(world, CountEvents);

        ReversalResult[] acts =
        [
            await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), DateTimeOffset.UtcNow.AddDays(7), Token),
            await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, 1), DateTimeOffset.UtcNow.AddDays(7), Token),
            await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, 3), DateTimeOffset.UtcNow.AddDays(7), Token),
            await world.ReversalRequests.DeclineAsync(new ReversalAct(1, request, second), Token),
            await world.ReversalRequests.AskAsync(1, 3, Draft(item, marker), Token),
            await world.ReversalRequests.AskAsync(1, 1, Draft(item, marker), Token),
        ];

        Assert.All(acts, act => Assert.Equal((ReversalOutcome.NotRecorded, CustodyOutcome.ChainBroken), (act.Outcome, act.Refusal?.Outcome)));
        Assert.Equal((0L, 1L, events), (await CountAsync(world, CountAuthorizations), await CountAsync(world, CountRequests), await CountAsync(world, CountEvents)));
        Assert.Equal(ReversalState.Pending, (await world.ReversalRequestReader.ReadAsync(1, request, Token))!.State);
    }

    [DatabaseFact]
    public async Task SecondAuthorizationWhileOneIsValidIsRefused()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (second, third) = (await SecondCuratorAsync(world), await SecondCuratorAsync(world, "zqx-curator-3"));
        var request = await AskedAsync(world);
        var until = DateTimeOffset.UtcNow.AddDays(7);

        var decided = await Task.WhenAll(
            world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), until, Token),
            world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, third), until, Token));

        Assert.Equal([ReversalOutcome.Recorded, ReversalOutcome.Closed], decided.Select(act => act.Outcome).Order());
        var refused = await Assert.ThrowsAsync<PostgresException>(() => ReversalRows.AuthorizeAsync(world, (1, request), second, "1 day"));
        Assert.Equal((ReversalRefusal, 1L), (refused.SqlState, await CountAsync(world, CountAuthorizations)));
        var declined = await world.ReversalRequests.DeclineAsync(new ReversalAct(1, request, third), Token);
        Assert.Equal(ReversalOutcome.Closed, declined.Outcome);
    }

    [DatabaseFact]
    public async Task SecondAuthorizationWhileTheFirstCommitsIsClosed()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (second, third) = (await SecondCuratorAsync(world), await SecondCuratorAsync(world, "zqx-curator-3"));
        var request = await AskedAsync(world);
        await using var first = await PreparedAuthorization.PrepareAsync(world, request, second);
        var clock = new PauseClock();

        var decided = new ReversalRequests(world.DataSource, world.Content, clock).AuthorizeAsync(new ReversalAct(1, request, third), DateTimeOffset.UtcNow.AddDays(7), Token);
        _ = await Task.WhenAny(clock.Waited, decided).WaitAsync(TimeSpan.FromMinutes(1), Token);
        await first.CommitAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        var answer = await decided;

        Assert.Equal((ReversalOutcome.Closed, null, 1L), (answer.Outcome, answer.Refusal?.Outcome, await CountAsync(world, CountAuthorizations)));
    }

    [DatabaseFact]
    public async Task AuthorizationWithATermInThePastIsRefused()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var request = await AskedAsync(world);
        var events = await CountAsync(world, CountEvents);

        var past = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), DateTimeOffset.UtcNow.AddMinutes(-1), Token);

        Assert.Equal((ReversalOutcome.TermInThePast, 0L, events), (past.Outcome, await CountAsync(world, CountAuthorizations), await CountAsync(world, CountEvents)));
        foreach (var term in new[] { "-1 day", "0 seconds" })
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => ReversalRows.AuthorizeAsync(world, (1, request), second, term));
            Assert.Equal(ReversalRefusal, refused.SqlState);
        }

        Assert.Equal((0L, ReversalState.Pending), (await CountAsync(world, CountAuthorizations), (await world.ReversalRequestReader.ReadAsync(1, request, Token))!.State));
    }

    [DatabaseFact]
    public async Task DatabaseCopyHasNoReadableRequestText()
    {
        await using var world = await ContentWorld.CreateAsync();
        string[] texts = [$"zqx-porque-{Guid.NewGuid():N}", $"zqx-uso-{Guid.NewGuid():N}"];
        var (item, marker) = await ItemAsync(world, 2);
        var asked = await world.ReversalRequests.AskAsync(2, 3, Draft(item, marker) with { Reason = texts[0], Use = texts[1] }, Token);
        var request = Assert.NotNull(asked.Id);

        var dump = await world.Database.DumpAsync();

        var stored = await world.StoredAsync(2, "SELECT justification FROM content.reversal_request WHERE id = @id", request);
        Assert.Equal("mapa-reversao/2/v1", EnvelopeCipher.LabelOf(stored));
        var read = await world.ReversalRequestReader.ReadTextAsync(new ReversalAct(2, request, 3), Token);
        Assert.Equal((ContentReadOutcome.Read, texts[0], texts[1]), (read.Outcome, read.Reason, read.Use));
        Assert.Equal([false, false, false, false], texts.SelectMany(text => new[] { text, Hex(text) }).Select(seeded => dump.Contains(seeded, StringComparison.Ordinal)));
    }

    private static string Hex(string text) => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(text));
}
