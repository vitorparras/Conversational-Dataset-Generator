using System.Globalization;
using Core.Host.Screens;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Core.Persistence.People;
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

    internal const string DayFormat = "yyyy-MM-dd";

    /// <summary>The last second of the day chosen; a fraction past it would be rounded by the database into the day after.</summary>
    private static readonly TimeOnly LastSecond = new(23, 59, 59);

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.ReversalDecide, DecisionAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapPost(ScreenPaths.ReversalDecide, DecideAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
    }

    private static Task<IResult> DecisionAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        if (request.RequestedBy == acting.Person)
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
        return await PageAsync(context, acting, request, shown);
    });

    /// <summary>
    /// Authorizes until the day chosen, or declines, in the name of the person of the session; a day that is not one, or is past, comes back with what to fix and nothing authorized.
    /// </summary>
    private static Task<IResult> DecideAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        var form = await RequestReader.FormAsync(context) ?? FormCollection.Empty;
        var (decision, until) = (form[DecisionField].ToString(), form[UntilField].ToString());
        var act = new ReversalAct(acting.Domain.Id, id, acting.Person);
        if (string.Equals(decision, Decline, StringComparison.Ordinal))
        {
            return await AnswerAsync(context, acting, request, await acting.Access.Requests.DeclineAsync(act, context.RequestAborted), (Declined, until));
        }

        if (!string.Equals(decision, Authorize, StringComparison.Ordinal) || !DateOnly.TryParseExact(until, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return await PageAsync(context, acting, request, Shown.PastDay, until);
        }

        var result = await acting.Access.Requests.AuthorizeAsync(act, new DateTimeOffset(day.ToDateTime(LastSecond), TimeSpan.Zero), context.RequestAborted);
        return await AnswerAsync(context, acting, request, result, (Authorized, until));
    });

    /// <summary>
    /// The confirmation of a decision recorded, marked so in the address, or the page again with why it was not; the gate lets only the curation through, and one whose function changed
    /// since answers as a route that is not there.
    /// </summary>
    private static async Task<IResult> AnswerAsync(HttpContext context, Acting acting, ListedReversal request, ReversalResult result, (string Mark, string Until) decision)
    {
        if (result.Outcome == ReversalOutcome.Recorded)
        {
            return PageRedirect.Of(context, $"{ScreenPaths.ReversalDecideOf(request.Id)}?{decision.Mark}");
        }

        if (result.Outcome == ReversalOutcome.OwnRequest)
        {
            return await PageAsync(context, acting, request, Shown.Own);
        }

        if (result.Outcome == ReversalOutcome.TermInThePast)
        {
            return await PageAsync(context, acting, request, Shown.PastDay, decision.Until);
        }

        if (result.Outcome == ReversalOutcome.Closed)
        {
            return await PageAsync(context, acting, await acting.Access.Reader.ReadAsync(acting.Domain.Id, request.Id, context.RequestAborted) ?? request, Shown.Open);
        }

        return result.Outcome == ReversalOutcome.NotRecorded
            ? await PageAsync(context, acting, request, Shown.NotWritten, decision.Until, StatusCodes.Status503ServiceUnavailable)
            : ScreenResult.NotFound(context);
    }

    /// <summary>The page of the decision; a request still to decide opens its text first, and a text the chain did not record the reading of is not shown and the page says why.</summary>
    private static async Task<IResult> PageAsync(HttpContext context, Acting acting, ListedReversal request, Shown shown, string? until = null, int status = StatusCodes.Status200OK)
    {
        ReversalJustification? text = null;
        if (shown is Shown.Open or Shown.PastDay && request.State is ReversalState.Pending or ReversalState.Expired)
        {
            text = await acting.Access.Reader.ReadTextAsync(new ReversalAct(acting.Domain.Id, request.Id, acting.Person), context.RequestAborted);
            if (text.Outcome != ContentReadOutcome.Read)
            {
                (text, shown, status) = (null, Shown.NotWritten, StatusCodes.Status503ServiceUnavailable);
            }
        }

        var offered = DateOnly.FromDateTime(acting.Desk.Time.GetUtcNow().UtcDateTime).AddDays(request.TermDays).ToString(DayFormat, CultureInfo.InvariantCulture);
        var page = ScreenResult.Of(context, typeof(ReversalDecisionScreen), ReversalDecisionScreen.TitleOf(request.Id, shown), new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalDecisionScreen.Request)] = request,
            [nameof(ReversalDecisionScreen.Text)] = text,
            [nameof(ReversalDecisionScreen.Requester)] = await acting.Desk.NameOfAsync(request.RequestedBy, context.RequestAborted),
            [nameof(ReversalDecisionScreen.Until)] = until ?? offered,
            [nameof(ReversalDecisionScreen.Showing)] = shown,
        });
        page.StatusCode = status;
        return page;
    }
}
