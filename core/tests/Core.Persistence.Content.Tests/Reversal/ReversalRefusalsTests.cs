using System.Globalization;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using static Core.Persistence.Tests.Reversal.ReversalAsks;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// The refusals of the storage of the reversal (issues 103 and 104, eventos-da-reversao.md): each one an event of its own, by who acted, pointing to the item and the marker, to the
/// domain and the class of the scope, or to the request, and none leaving a row; the scope is judged before the function and the function before the markers of the item, and who
/// authorizes before whose request it is. A request declined is never authorized, and its text opens only once the chain recorded the reading.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalRefusalsTests
{
    private const string Readings =
        "SELECT count(*) FROM public.custody_event WHERE item_kind = 'reversal_request' AND item_id = @id AND step = 'reversal_request_read' AND reason = 'administrative_act' "
        + "AND reference IS NULL AND actor_person_id = @person";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<long, string, string, ReversalOutcome, string> Asks => new()
    {
        { 3, "{item}, 204", "{marker}", ReversalOutcome.TooWide, "domain|1|reversal_request_refused|reversal_scope|varios-itens|3" },
        { 3, "{item}", "{marker}, LUGAR_00000001", ReversalOutcome.TooWide, "domain|1|reversal_request_refused|reversal_scope|varios-marcadores|3" },
        { 3, "", "{marker}", ReversalOutcome.TooWide, "domain|1|reversal_request_refused|reversal_scope|dominio|3" },
        { 3, "{item}", "", ReversalOutcome.TooWide, "domain|1|reversal_request_refused|reversal_scope|dominio|3" },
        { 3, "{item}", "LUGAR_00000001", ReversalOutcome.MarkerNotInItem, "item_text|{item}|reversal_request_refused|reversal_scope|LUGAR_00000001|3" },
        { 1, "{item}", "{marker}", ReversalOutcome.NotCuration, "item_text|{item}|reversal_request_refused|reversal_function|{marker}|1" },
        { 1, "{item}, 204", "{marker}", ReversalOutcome.TooWide, "domain|1|reversal_request_refused|reversal_scope|varios-itens|1" },
        { 1, "{item}", "LUGAR_00000001", ReversalOutcome.NotCuration, "item_text|{item}|reversal_request_refused|reversal_function|LUGAR_00000001|1" },
    };

    [DatabaseTheory]
    [MemberData(nameof(Asks))]
    public async Task EachRefusedRequestIsAnEventOfItsOwnAndLeavesNoRow(long person, string items, string markers, ReversalOutcome outcome, string recorded)
    {
        await using var world = await ContentWorld.CreateAsync();
        var (item, marker) = await ItemAsync(world, 1);
        var events = await CountAsync(world, CountEvents);

        var asked = await world.ReversalRequests.AskAsync(1, person, Draft(item, marker) with { Item = Filled(items, item, marker), Marker = Filled(markers, item, marker) }, Token);

        Assert.Equal((outcome, null), (asked.Outcome, asked.Id));
        Assert.Equal((Filled(recorded, item, marker), 0L, events + 1), (await LastEventAsync(world), await CountAsync(world, CountRequests), await CountAsync(world, CountEvents)));
    }

    [DatabaseFact]
    public async Task EachRefusedAuthorizationIsAnEventOfItsOwnAndLeavesNoRow()
    {
        await using var world = await ContentWorld.CreateAsync();
        var request = await AskedAsync(world);
        var until = DateTimeOffset.UtcNow.AddDays(7);

        var own = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, 3), until, Token);
        var ownEvent = await LastEventAsync(world);
        var annotator = await world.ReversalRequests.DeclineAsync(new ReversalAct(1, request, 1), Token);
        var annotatorEvent = await LastEventAsync(world);
        await world.Database.RunAsOwnerAsync("UPDATE public.person SET function_name = 'Anotação' WHERE id = 3");
        var ownWithoutFunction = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, 3), until, Token);

        Assert.Equal(
            (ReversalOutcome.OwnRequest, ReversalOutcome.NotCuration, ReversalOutcome.NotCuration),
            (own.Outcome, annotator.Outcome, ownWithoutFunction.Outcome));
        Assert.Equal(
            ($"reversal_request|{request}|reversal_authorization_refused|reversal_self||3", $"reversal_request|{request}|reversal_authorization_refused|reversal_function||1",
                $"reversal_request|{request}|reversal_authorization_refused|reversal_function||3"),
            (ownEvent, annotatorEvent, await LastEventAsync(world)));
        Assert.Equal((0L, ReversalState.Pending), (await CountAsync(world, CountAuthorizations), (await world.ReversalRequestReader.ReadAsync(1, request, Token))!.State));
    }

    [DatabaseFact]
    public async Task DeclinedRequestIsNeverAuthorized()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var request = await AskedAsync(world);

        var declined = await world.ReversalRequests.DeclineAsync(new ReversalAct(1, request, second), Token);
        var declinedEvent = await LastEventAsync(world);
        var events = await CountAsync(world, CountEvents);
        var authorized = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), DateTimeOffset.UtcNow.AddDays(7), Token);

        Assert.Equal((ReversalOutcome.Recorded, ReversalOutcome.Closed), (declined.Outcome, authorized.Outcome));
        Assert.Equal($"reversal_request|{request}|reversal_declined|administrative_act||{second}", declinedEvent);
        Assert.Equal((events, ReversalState.Declined), (await CountAsync(world, CountEvents), (await world.ReversalRequestReader.ReadAsync(1, request, Token))!.State));
        var refused = await Assert.ThrowsAsync<PostgresException>(() => ReversalRows.AuthorizeAsync(world, (1, request), second, "30 days"));
        Assert.Equal(("CE007", 0L), (refused.SqlState, await CountAsync(world, CountAuthorizations)));
    }

    [DatabaseFact]
    public async Task RequestTextOpensOnlyAfterItsReadingIsRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var request = await AskedAsync(world);

        var read = await world.ReversalRequestReader.ReadTextAsync(new ReversalAct(1, request, second), Token);

        Assert.Equal((ContentReadOutcome.Read, "zqx-porque", "zqx-uso"), (read.Outcome, read.Reason, read.Use));
        Assert.Equal(1L, (long)(await world.ScalarAsync(Readings, ("id", request), ("person", second)))!);
        await world.BreakTheChainAsync();
        var events = await CountAsync(world, CountEvents);

        var broken = await world.ReversalRequestReader.ReadTextAsync(new ReversalAct(1, request, second), Token);

        Assert.Equal((ContentReadOutcome.NotRecorded, null, null, events), (broken.Outcome, broken.Reason, broken.Use, await CountAsync(world, CountEvents)));
    }

    private static string Filled(string? text, long item, string marker) =>
        (text ?? string.Empty).Replace("{item}", item.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("{marker}", marker, StringComparison.Ordinal);
}
