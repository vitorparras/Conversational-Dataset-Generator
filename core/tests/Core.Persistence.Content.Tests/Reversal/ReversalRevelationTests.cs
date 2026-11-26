using System.Globalization;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using static Core.Persistence.Tests.Reversal.ReversalReveals;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// The revelation of a marker (issues 105 and 104): recorded with its event and committed before the map of the item is opened, one event for each, denied with the reason when the
/// authorization lapsed or served already, and an authorization that serves its own request once.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalRevelationTests
{
    private const string WhatTheEventReaches =
        "SELECT r.requested_by || '|' || a.authorized_by || '|' || r.marker || '|' || r.item_id || '|' || (r.justification IS NOT NULL) FROM content.reversal_revelation v "
        + "JOIN content.reversal_authorization a ON a.id = v.authorization_id JOIN content.reversal_request r ON r.id = a.request_id WHERE v.id = @id";

    private const string RevealedThenRead =
        "SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE step IN ('reversal_revealed', 'reversal_entries_read')";

    private const string MarkersRevealed =
        "SELECT string_agg(r.marker || '>' || e.reference, ',' ORDER BY e.sequence) FROM public.custody_event e JOIN content.reversal_revelation v ON v.id = e.item_id "
        + "JOIN content.reversal_authorization a ON a.id = v.authorization_id JOIN content.reversal_request r ON r.id = a.request_id WHERE e.step = 'reversal_revealed'";

    private const string AuthorizationOfRequest = "SELECT id FROM content.reversal_authorization WHERE request_id = @request ORDER BY id DESC LIMIT 1";

    private const string RevealOnTheSide =
        "WITH v AS (INSERT INTO content.reversal_revelation (domain_id, authorization_id) VALUES (1, @authorization) RETURNING id) "
        + "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "SELECT 3, false, 'reversal_revelation', v.id, 'reversal_revealed', 'administrative_act', @authorization::text FROM v";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task RevelationEventIsInTheChainBeforeTheValueLeaves()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        await using var holder = new NpgsqlConnection(world.Database.OwnerConnectionString);
        var hold = await HoldTheMapAsync(holder);

        var revealing = world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token);
        var seen = await RevelationSeenAsync(world, revealing);
        var pendingWhenSeen = !revealing.IsCompleted;
        await hold.RollbackAsync(Token);
        var revealed = await revealing;

        Assert.True(pendingWhenSeen, "a identidade saiu antes de o evento da revelação estar na cadeia");
        Assert.EndsWith($"|reversal_revealed|administrative_act|{authorized.Authorization}|3", seen, StringComparison.Ordinal);
        var revelation = long.Parse(seen.Split('|')[1], CultureInfo.InvariantCulture);
        var second = await world.PersonAsync($"zqx-curator-{authorized.Marker}");
        Assert.Equal($"3|{second}|{authorized.Marker}|{authorized.Item}|true", await world.InDomainAsync(1, WhatTheEventReaches, ("id", revelation)));
        Assert.Equal((ReversalOutcome.Recorded, PseudonymizedTexts.OriginalOf(authorized.Marker)), (revealed.Outcome, revealed.Identity));
        _ = Assert.NotNull(revealed.RevealedAt);
        Assert.Equal("reversal_revealed,reversal_entries_read", await world.ScalarAsync(RevealedThenRead));
    }

    [DatabaseFact]
    public async Task FailureToRecordTheEventRevealsNothingAndLeavesTheRequestOpen()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        await RefuseInTheChainAsync(world, "reversal_revealed");

        var revealed = await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token);

        Assert.Equal((ReversalOutcome.NotRecorded, CustodyOutcome.ChainBroken, null), (revealed.Outcome, revealed.Refusal?.Outcome, revealed.Identity));
        Assert.Equal((0L, 0L, 0L), (await ReversalAsks.CountAsync(world, CountRevelations), await CountOfStepAsync(world, "reversal_revealed"),
            await CountOfStepAsync(world, "reversal_entries_read")));
        var standing = await world.ReversalRequestReader.ReadAsync(1, authorized.Request, Token);
        Assert.Equal((ReversalState.Authorized, null), (standing?.State, standing?.RevealedAt));
    }

    [DatabaseFact]
    public async Task RevelationEventCannotBeRemovedOrChangedAndTheAttemptIsDetected()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        var revealed = await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token);
        Assert.Equal(ReversalOutcome.Recorded, revealed.Outcome);
        string[] onTheRevelation =
        [
            "UPDATE content.reversal_revelation SET revealed_at = revealed_at - interval '1 day'", "DELETE FROM content.reversal_revelation",
            "TRUNCATE content.reversal_revelation",
        ];
        string[] onTheEvent =
        [
            "UPDATE public.custody_event SET reference = '1' WHERE step = 'reversal_revealed'", "DELETE FROM public.custody_event WHERE step = 'reversal_revealed'",
        ];

        foreach (var statement in onTheRevelation)
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, statement));
            Assert.Equal(CustodyStatements.BatchSqlState, refused.SqlState);
        }

        await world.Database.RunAsOwnerAsync("GRANT UPDATE, DELETE ON public.custody_event TO owner");
        foreach (var statement in onTheEvent)
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(statement));
            Assert.Equal(CustodyStatements.ImmutableSqlState, refused.SqlState);
        }

        var sequence = (long)(await world.ScalarAsync("SELECT sequence FROM public.custody_event WHERE step = 'reversal_revealed'"))!;
        await world.Database.ExecuteAsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET reference = reference || '9' WHERE step = 'reversal_revealed'; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed");
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(Token);
        Assert.Equal(sequence, verified.FirstInconsistent?.Sequence);
        Assert.Equal((1L, 1L), (await ReversalAsks.CountAsync(world, CountRevelations), await CountOfStepAsync(world, "reversal_revealed")));
    }

    [DatabaseFact]
    public async Task TwoRevelationsAreTwoEventsEachWithItsMarker()
    {
        await using var world = await ContentWorld.CreateAsync();
        var first = await AuthorizedAsync(world);
        var second = await AuthorizedAsync(world);

        var revealedFirst = await world.ReversalRevelations.RevealAsync(ByWhoAsked(first.Request), Token);
        var revealedSecond = await world.ReversalRevelations.RevealAsync(ByWhoAsked(second.Request), Token);

        Assert.Equal((PseudonymizedTexts.OriginalOf(first.Marker), PseudonymizedTexts.OriginalOf(second.Marker)), (revealedFirst.Identity, revealedSecond.Identity));
        Assert.Equal($"{first.Marker}>{first.Authorization},{second.Marker}>{second.Authorization}", await world.InDomainAsync(1, MarkersRevealed));
        Assert.Equal(2L, await CountOfStepAsync(world, "reversal_entries_read"));
    }

    [DatabaseFact]
    public async Task ExpiredAuthorizationDeniesAsIfItNeverExistedAndIsRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var request = await ReversalAsks.AskedAsync(world);
        var second = await ReversalAsks.SecondCuratorAsync(world);
        await ReversalRows.ExpiredAsync(world, (1, request), second);
        var authorization = (long)(await world.InDomainAsync(1, AuthorizationOfRequest, ("request", request)))!;

        var revealed = await world.ReversalRevelations.RevealAsync(ByWhoAsked(request), Token);

        Assert.Equal((ReversalOutcome.Expired, null, null), (revealed.Outcome, revealed.Identity, revealed.RevealedAt));
        Assert.Equal($"reversal_authorization|{authorization}|reversal_denied|reversal_expired||3", await ReversalAsks.LastEventAsync(world));
        Assert.Equal((0L, 0L), (await ReversalAsks.CountAsync(world, CountRevelations), await CountOfStepAsync(world, "reversal_entries_read")));
        var onTheSide = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, RevealOnTheSide, ("authorization", authorization)));
        Assert.Equal(CustodyStatements.ReversalSqlState, onTheSide.SqlState);
    }

    [DatabaseFact]
    public async Task AuthorizationServesOnlyItsOwnRequest()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        Assert.Equal(ReversalOutcome.Recorded, (await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token)).Outcome);
        var other = await world.ReversalRequests.AskAsync(1, 3, ReversalAsks.Draft(authorized.Item, authorized.Marker), Token);
        var events = await ReversalAsks.CountAsync(world, ReversalAsks.CountEvents);

        var onTheOther = await world.ReversalRevelations.RevealAsync(ByWhoAsked(Assert.NotNull(other.Id)), Token);
        var again = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, RevealOnTheSide, ("authorization", authorized.Authorization)));

        Assert.Equal((ReversalOutcome.Closed, null), (onTheOther.Outcome, onTheOther.Identity));
        Assert.Equal((PostgresErrorCodes.UniqueViolation, "reversal_revelation_once"), (again.SqlState, again.ConstraintName));
        Assert.Equal((1L, events), (await ReversalAsks.CountAsync(world, CountRevelations), await ReversalAsks.CountAsync(world, ReversalAsks.CountEvents)));
    }

    [DatabaseFact]
    public async Task RevealedRequestTakesNoNewAuthorizationOnceItLapses()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (item, marker) = await ReversalAsks.ItemAsync(world, 1);
        var request = Assert.NotNull((await world.ReversalRequests.AskAsync(1, 3, ReversalAsks.Draft(item, marker), Token)).Id);
        var second = await ReversalAsks.SecondCuratorAsync(world, $"zqx-curator-{marker}");
        var until = DateTimeOffset.UtcNow.AddSeconds(3);
        Assert.Equal(ReversalOutcome.Recorded, (await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), until, Token)).Outcome);
        Assert.Equal(ReversalOutcome.Recorded, (await world.ReversalRevelations.RevealAsync(ByWhoAsked(request), Token)).Outcome);
        var lapse = until - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(300);
        await Task.Delay(lapse > TimeSpan.Zero ? lapse : TimeSpan.Zero, Token);
        var events = await ReversalAsks.CountAsync(world, ReversalAsks.CountEvents);

        var renewed = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), DateTimeOffset.UtcNow.AddDays(7), Token);

        Assert.Equal((ReversalOutcome.Closed, ReversalState.Revealed), (renewed.Outcome, (await world.ReversalRequestReader.ReadAsync(1, request, Token))!.State));
        Assert.Equal((1L, events), (await ReversalAsks.CountAsync(world, CountRevelations), await ReversalAsks.CountAsync(world, ReversalAsks.CountEvents)));
    }

    [DatabaseFact]
    public async Task RequestOfAnotherOrNoneIsNotFoundWithNoEvent()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        var second = await world.PersonAsync($"zqx-curator-{authorized.Marker}");
        var events = await ReversalAsks.CountAsync(world, ReversalAsks.CountEvents);

        var byAnother = await world.ReversalRevelations.RevealAsync(new ReversalAct(1, authorized.Request, second), Token);
        var ofNone = await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request + 100), Token);

        Assert.Equal((ReversalOutcome.NotFound, ReversalOutcome.NotFound), (byAnother.Outcome, ofNone.Outcome));
        Assert.Equal((null, null), (byAnother.Identity, ofNone.Identity));
        Assert.Equal((0L, events), (await ReversalAsks.CountAsync(world, CountRevelations), await ReversalAsks.CountAsync(world, ReversalAsks.CountEvents)));
    }

    [DatabaseFact]
    public async Task FailureToRecordTheReadingOfTheMapRevealsNothingAndKeepsTheRevelation()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        await RefuseInTheChainAsync(world, "reversal_entries_read");

        var revealed = await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token);

        Assert.Equal((ReversalOutcome.NotRecorded, null), (revealed.Outcome, revealed.Identity));
        _ = Assert.NotNull(revealed.RevealedAt);
        Assert.Equal(CustodyOutcome.ChainBroken, revealed.Refusal?.Outcome);
        Assert.Equal((1L, 1L, 0L), (await ReversalAsks.CountAsync(world, CountRevelations), await CountOfStepAsync(world, "reversal_revealed"),
            await CountOfStepAsync(world, "reversal_entries_read")));
    }

    [DatabaseFact]
    public async Task ConflictOnRecordingTheReadingOfTheMapIsTriedAgainAndRevealsTheIdentity()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        await ConflictOnceInTheChainAsync(world, "reversal_entries_read");

        var revealed = await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token);

        Assert.Equal((ReversalOutcome.Recorded, PseudonymizedTexts.OriginalOf(authorized.Marker)), (revealed.Outcome, revealed.Identity));
        Assert.Equal(2L, (long)(await world.ScalarAsync(ConflictAttempts))!);
        Assert.Equal((1L, 1L), (await CountOfStepAsync(world, "reversal_revealed"), await CountOfStepAsync(world, "reversal_entries_read")));
    }

    [DatabaseFact]
    public async Task MapGoneAfterTheRevelationIsRecordedIsDeniedAsEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);
        await world.Database.ExecuteAsSuperuserAsync(
            "ALTER TABLE content.reversal_map DISABLE TRIGGER map_emptied_with_item, DISABLE TRIGGER map_change_needs_event; "
            + "UPDATE content.reversal_map SET emptied_at = now(), entries = NULL, dek_epoch = NULL, dek_wrapped = NULL WHERE item_id = @item; "
            + "ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER map_emptied_with_item, ENABLE ALWAYS TRIGGER map_change_needs_event",
            ("item", authorized.Item));

        var revealed = await world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token);

        Assert.Equal((ReversalOutcome.Emptied, null), (revealed.Outcome, revealed.Identity));
        _ = Assert.NotNull(revealed.RevealedAt);
        Assert.Equal((1L, 0L), (await ReversalAsks.CountAsync(world, CountRevelations), await CountOfStepAsync(world, "reversal_entries_read")));
    }

    [DatabaseFact]
    public async Task NullArgumentOfTheRevelationIsRefusedByItsName()
    {
        await using var world = await ContentWorld.CreateAsync();

        string?[] names =
        [
            Assert.Throws<ArgumentNullException>(() => new ReversalRevelations(null!, world.Reader, TimeProvider.System)).ParamName,
            Assert.Throws<ArgumentNullException>(() => new ReversalRevelations(world.DataSource, null!, TimeProvider.System)).ParamName,
            Assert.Throws<ArgumentNullException>(() => new ReversalRevelations(world.DataSource, world.Reader, null!)).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => world.ReversalRevelations.RevealAsync(null!, Token))).ParamName,
        ];

        Assert.Equal(["dataSource", "reader", "time", "act"], names);
    }

    [DatabaseFact]
    public async Task TwoRevealPostsAtOnceRevealOnce()
    {
        await using var world = await ContentWorld.CreateAsync();
        var authorized = await AuthorizedAsync(world);

        var both = await Task.WhenAll(
            world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token), world.ReversalRevelations.RevealAsync(ByWhoAsked(authorized.Request), Token));

        Assert.Equal([ReversalOutcome.Recorded, ReversalOutcome.AlreadyRevealed], both.Select(revealed => revealed.Outcome).Order());
        _ = Assert.Single(both, revealed => revealed.Identity is not null);
        Assert.Equal((1L, 1L, 1L), (await ReversalAsks.CountAsync(world, CountRevelations), await CountOfStepAsync(world, "reversal_revealed"),
            await CountOfStepAsync(world, "reversal_entries_read")));
        Assert.Equal($"reversal_authorization|{authorized.Authorization}|reversal_denied|reversal_used||3", await world.ScalarAsync(
            ReversalAsks.LastEvent.Replace("FROM public.custody_event", "FROM public.custody_event WHERE step = 'reversal_denied'", StringComparison.Ordinal)));
    }
}
