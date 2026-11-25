using Core.Host.Screens;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Reversal;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Problem = Core.Host.Screens.ReversalRequestScreen.Problem;

namespace Core.Host.Endpoints;

/// <summary>
/// The request to reverse a marker: the form, and its POST, which either leads with a 303 to the request sent or comes back with what to fix, as typed. The requests decide what is refused and
/// record it; the page only says it.
/// </summary>
internal static class ReversalAskEndpoint
{
    private static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

    internal static Task<IResult> FormAsync(HttpContext context, [FromServices] ReversalDesk? desk) =>
        ReversalScreensEndpoint.ActingAsync(context, desk, _ => Task.FromResult<IResult>(Asking(context, new ReversalRequestDraft(), new Dictionary<ReversalRequestField, Problem>())));

    /// <summary>Asks in the name of the person of the session and leads to the request sent; what is missing, too wide or not in the item comes back on the form, as typed.</summary>
    internal static Task<IResult> AskAsync(HttpContext context, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        var form = await RequestReader.FormAsync(context) ?? FormCollection.Empty;
        var draft = new ReversalRequestDraft
        {
            Item = form["item"].ToString(),
            Marker = form["marcador"].ToString(),
            Term = form["prazo"].ToString(),
            Reason = form["motivo"].ToString(),
            Use = form["uso"].ToString(),
        };
        return AnswerOf(context, draft, await acting.Access.Requests.AskAsync(acting.Domain.Id, acting.Actor, draft, context.RequestAborted));
    });

    /// <summary>
    /// The request sent, or the form again with what the requests refused; the gate lets only the curation through, and one whose function changed since answers as a route that is not there.
    /// </summary>
    private static IResult AnswerOf(HttpContext context, ReversalRequestDraft draft, ReversalResult result)
    {
        if (result is { Outcome: ReversalOutcome.Recorded, Id: { } id })
        {
            return PageRedirect.Of(context, $"{ScreenPaths.ReversalMineOf(id)}?{ReversalScreensEndpoint.Sent}");
        }

        if (result.Outcome == ReversalOutcome.Incomplete)
        {
            return Asking(context, draft, MissingIn(draft, result.Missing));
        }

        if (result.Outcome == ReversalOutcome.TooWide)
        {
            return Asking(context, draft, WideIn(draft, result.Scope));
        }

        if (result.Outcome == ReversalOutcome.MarkerNotInItem)
        {
            return Asking(context, draft, new Dictionary<ReversalRequestField, Problem> { [ReversalRequestField.Marker] = Problem.NotInItem });
        }

        return result.Outcome == ReversalOutcome.NotRecorded
            ? Asking(context, draft, new Dictionary<ReversalRequestField, Problem>(), StatusCodes.Status503ServiceUnavailable)
            : ScreenResult.NotFound(context);
    }

    /// <summary>What the requests found missing, and the item or the marker left blank, which they take for the whole domain only once the rest is there.</summary>
    private static Dictionary<ReversalRequestField, Problem> MissingIn(ReversalRequestDraft draft, IEnumerable<ReversalRequestField> missing) =>
        missing.Concat(new[] { (Field: ReversalRequestField.Item, Typed: draft.Item), (Field: ReversalRequestField.Marker, Typed: draft.Marker) }.Where(typed => Blank(typed.Typed))
            .Select(typed => typed.Field)).Distinct().ToDictionary(field => field, _ => Problem.Missing);

    /// <summary>The field that made the request reach too far: more than one item or marker, or the one left blank, which reaches the whole domain.</summary>
    private static Dictionary<ReversalRequestField, Problem> WideIn(ReversalRequestDraft draft, ReversalScope? scope) => scope switch
    {
        ReversalScope.ManyItems => new() { [ReversalRequestField.Item] = Problem.ManyItems },
        ReversalScope.ManyMarkers => new() { [ReversalRequestField.Marker] = Problem.ManyMarkers },
        ReversalScope.Domain or null => new() { [Blank(draft.Item) ? ReversalRequestField.Item : ReversalRequestField.Marker] = Problem.WholeDomain },
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "The scope is not one the requests refuse."),
    };

    private static bool Blank(string? typed) => string.IsNullOrEmpty(typed?.Trim(Separators));

    private static RazorComponentResult<ScreenFrame> Asking(
        HttpContext context, ReversalRequestDraft draft, IReadOnlyDictionary<ReversalRequestField, Problem> problems, int status = StatusCodes.Status200OK)
    {
        var page = ScreenResult.Of(context, typeof(ReversalRequestScreen), ReversalRequestScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalRequestScreen.Draft)] = draft,
            [nameof(ReversalRequestScreen.Problems)] = problems,
            [nameof(ReversalRequestScreen.NotWritten)] = status != StatusCodes.Status200OK,
        });
        page.StatusCode = status;
        return page;
    }
}
