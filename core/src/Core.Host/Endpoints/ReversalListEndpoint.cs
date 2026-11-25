using System.Globalization;
using Core.Host.Screens;
using Core.Persistence.Content.Reversal;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>The lists of the reversal, of the requests of the person and of those of the others that wait for a decision, and each request of the person as it stands.</summary>
internal static class ReversalListEndpoint
{
    /// <summary>The requests of the person, or those of the others that wait for a decision, narrowed as the address says, one page of them.</summary>
    internal static Task<IResult> ListAsync(HttpContext context, ReversalDesk? desk, bool mine) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        var query = context.Request.Query;
        var (item, situation) = (query[ReversalListScreen.ItemKey].ToString(), query[ReversalListScreen.StateKey].ToString());
        var number = int.TryParse(query[ReversalListScreen.PageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) && asked > 0 ? asked : 1;
        var listing = new ReversalListing
        {
            DomainId = acting.Domain.Id,
            PersonId = acting.Actor,
            Mine = mine,
            Page = number,
            ItemId = !mine && long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null,
            State = mine ? ReversalListScreen.StateOf(situation) : null,
        };
        return ScreenResult.Of(context, typeof(ReversalListScreen), mine ? ReversalListScreen.MineTitle : ReversalListScreen.AwaitingTitle, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalListScreen.Mine)] = mine,
            [nameof(ReversalListScreen.Found)] = await acting.Access.Reader.ListAsync(listing, context.RequestAborted),
            [nameof(ReversalListScreen.Page)] = number,
            [nameof(ReversalListScreen.Item)] = mine ? null : item,
            [nameof(ReversalListScreen.Situation)] = mine ? situation : null,
        });
    });

    /// <summary>A request of the person, as it stands now; the request of someone else answers as a route that is not there.</summary>
    internal static Task<IResult> FollowAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request || request.RequestedBy != acting.Actor)
        {
            return ScreenResult.NotFound(context);
        }

        var sent = context.Request.Query.ContainsKey(ReversalScreensEndpoint.Sent) && request.State == ReversalState.Pending;
        return ScreenResult.Of(context, typeof(ReversalScreen), ReversalScreen.TitleOf(id, sent), new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalScreen.Request)] = request,
            [nameof(ReversalScreen.Domain)] = acting.Domain.Name,
            [nameof(ReversalScreen.Sent)] = sent,
        });
    });
}
