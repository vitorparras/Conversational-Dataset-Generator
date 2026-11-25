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
    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
}
