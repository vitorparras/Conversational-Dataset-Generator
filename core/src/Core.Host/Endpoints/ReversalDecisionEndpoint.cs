using System.Globalization;
using Core.Host.Screens;
using Core.Persistence.Content.Reversal;
using Microsoft.AspNetCore.Mvc;
using Acting = Core.Host.Endpoints.ReversalScreensEndpoint.Acting;
using Shown = Core.Host.Screens.ReversalDecisionScreen.Shown;

namespace Core.Host.Endpoints;

/// <summary>
/// The decision on a request of another person of the curation of the domain: the page opens why it asks and what for only after the chain recorded the reading, and the POST authorizes until
/// the end of the day chosen, in universal time, or declines, and leads with a 303 to the confirmation of that request; whoever asked is led to the request as it follows it, and a POST of its own
/// is refused by the requests and said on the page.
/// </summary>
internal static class ReversalDecisionEndpoint
{
    /// <summary>The marks in the address of a request just decided, which the page confirms only when the request stands so.</summary>
    internal const string Authorized = "autorizado";

    internal const string Declined = "recusado";

    /// <summary>The fields of the decision: what is decided, and the day, as the field of a date sends it, the authorization is valid until.</summary>
    internal const string DecisionField = "decisao";

    internal const string UntilField = "validade";

    internal const string Authorize = "autorizar";

    internal const string Decline = "recusar";


    /// <summary>The last second of the day chosen; a fraction past it would be rounded by the database into the day after.</summary>
    private static readonly TimeOnly LastSecond = new(23, 59, 59);

    internal static Task<IResult> DecisionAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        if (request.RequestedBy == acting.Actor)
        {
            return PageRedirect.Of(context, ScreenPaths.ReversalMineOf(id));
        }

        var query = context.Request.Query;
        var shown = (request.State, query.ContainsKey(Authorized), query.ContainsKey(Declined)) switch
        {
            (ReversalState.Authorized, true, _) => Shown.Authorized,
            (ReversalState.Declined, _, true) => Shown.Declined,
            _ => Shown.Open,
        };
        return await ReversalDecisionPage.OfAsync(context, acting, request, new(shown));
    });

    /// <summary>
    /// Authorizes until the day chosen, or declines, in the name of the person of the session; a day that is not one, or is past, comes back with what to fix and nothing authorized.
    /// </summary>
    internal static Task<IResult> DecideAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        var form = await RequestReader.FormAsync(context) ?? FormCollection.Empty;
        var (decision, until) = (form[DecisionField].ToString(), form[UntilField].ToString());
        var act = new ReversalAct(acting.Domain.Id, id, acting.Actor);
        if (request.RequestedBy == acting.Actor)
        {
            // Whatever the form holds, a decision on a request of one's own goes to the requests, which refuse it before the day and record the attempt.
            return await AnswerAsync(context, acting, request, (await acting.Access.Requests.AuthorizeAsync(act, DateTimeOffset.UnixEpoch, context.RequestAborted), Authorized, until));
        }

        if (string.Equals(decision, Decline, StringComparison.Ordinal))
        {
            return await AnswerAsync(context, acting, request, (await acting.Access.Requests.DeclineAsync(act, context.RequestAborted), Declined, until));
        }

        var authorizing = string.Equals(decision, Authorize, StringComparison.Ordinal);
        if (!authorizing || !DateOnly.TryParseExact(until, ReversalDecisionPage.DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return await ReversalDecisionPage.OfAsync(context, acting, request, new(Shown.PastDay, until));
        }

        var result = await acting.Access.Requests.AuthorizeAsync(act, new DateTimeOffset(day.ToDateTime(LastSecond), TimeSpan.Zero), context.RequestAborted);
        return await AnswerAsync(context, acting, request, (result, Authorized, until));
    });

    /// <summary>
    /// The confirmation of a decision recorded, marked so in the address, or the page again with why it was not; the gate lets only the curation through, and one whose function changed
    /// since answers as a route that is not there.
    /// </summary>
    private static async Task<IResult> AnswerAsync(HttpContext context, Acting acting, ListedReversal request, (ReversalResult Result, string Mark, string Until) decision)
    {
        if (decision.Result.Outcome == ReversalOutcome.Recorded)
        {
            return PageRedirect.Of(context, $"{ScreenPaths.ReversalDecideOf(request.Id)}?{decision.Mark}");
        }

        if (decision.Result.Outcome == ReversalOutcome.OwnRequest)
        {
            return await ReversalDecisionPage.OfAsync(context, acting, request, new(Shown.Own));
        }

        if (decision.Result.Outcome == ReversalOutcome.TermInThePast)
        {
            return await ReversalDecisionPage.OfAsync(context, acting, request, new(Shown.PastDay, decision.Until));
        }

        if (decision.Result.Outcome == ReversalOutcome.Closed)
        {
            return await ReversalDecisionPage.OfAsync(context, acting, await acting.Access.Reader.ReadAsync(acting.Domain.Id, request.Id, context.RequestAborted) ?? request, new(Shown.Open));
        }

        return decision.Result.Outcome == ReversalOutcome.NotRecorded
            ? await ReversalDecisionPage.OfAsync(context, acting, request, new(Shown.NotWritten, decision.Until, StatusCodes.Status503ServiceUnavailable))
            : ScreenResult.NotFound(context);
    }
}
