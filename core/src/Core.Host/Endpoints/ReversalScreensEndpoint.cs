using System.Globalization;
using Core.Host.Screens;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Reversal;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Problem = Core.Host.Screens.ReversalRequestScreen.Problem;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the request to reverse a marker and of its authorization (issues 614 and 615), reached only by the curation of the collection with a domain granted: the request, whose POST
/// either leads with a 303 to the request sent or comes back with what to fix, the requests of the person and each of them, and the requests of the others that wait for a decision, decided on
/// a page of their own. The requests decide what is refused and record it; the screens only say it, and no screen shows an identity.
/// </summary>
internal static class ReversalScreensEndpoint
{
    /// <summary>The mark in the address of a request just sent, which titles it so.</summary>
    internal const string Sent = "enviado";

    private const int AskPlace = 23;
    private const int AuthorizePlace = 24;
    private const int MinePlace = 25;
    private static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.ReversalAsk, AskFormAsync).WithMetadata(ScreenAccess.Of(ReversalRequestScreen.Title, AskPlace, PersonFunctions.Curation));
        _ = endpoints.MapPost(ScreenPaths.ReversalAsk, AskAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.ReversalAuthorize, (HttpContext context, [FromServices] ReversalDesk? desk) => ListAsync(context, desk, mine: false))
            .WithMetadata(ScreenAccess.Of(ReversalListScreen.AwaitingTitle, AuthorizePlace, PersonFunctions.Curation));
        ReversalDecisionEndpoint.Map(endpoints);
        _ = endpoints.MapGet(ScreenPaths.ReversalMine, (HttpContext context, [FromServices] ReversalDesk? desk) => ListAsync(context, desk, mine: true))
            .WithMetadata(ScreenAccess.Of(ReversalListScreen.MineTitle, MinePlace, PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.ReversalMineOne, FollowAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
    }

    /// <summary>
    /// Runs a step in the name of the person of the session, over the requests opened with the key read now: no database or no key leaves the page unavailable, and no domain granted answers
    /// as a route that is not there.
    /// </summary>
    internal static async Task<IResult> ActingAsync(HttpContext context, ReversalDesk? desk, Func<Acting, Task<IResult>> step)
    {
        var granted = context.Features.Get<ScreenReach>()?.Granted.FirstOrDefault();
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (granted is null || RequestReader.SessionOf(context) is not { } person || await desk.DomainIdOfAsync(granted, context.RequestAborted) is not { } id)
        {
            return ScreenResult.NotFound(context);
        }

        using var access = await desk.OpenAsync(context.RequestAborted);
        return access is null ? ScreenResult.Unavailable(context) : await step(new Acting(desk, access, new EmptyingScreensEndpoint.ActiveDomain(granted, id), person));
    }

    private static Task<IResult> AskFormAsync(HttpContext context, [FromServices] ReversalDesk? desk) =>
        ActingAsync(context, desk, _ => Task.FromResult<IResult>(Asking(context, new ReversalRequestDraft(), new Dictionary<ReversalRequestField, Problem>())));

    /// <summary>Asks in the name of the person of the session and leads to the request sent; what is missing, too wide or not in the item comes back on the form, as typed.</summary>
    private static Task<IResult> AskAsync(HttpContext context, [FromServices] ReversalDesk? desk) => ActingAsync(context, desk, async acting =>
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
        return AnswerOf(context, draft, await acting.Access.Requests.AskAsync(acting.Domain.Id, acting.Person, draft, context.RequestAborted));
    });

    /// <summary>
    /// The request sent, or the form again with what the requests refused; the gate lets only the curation through, and one whose function changed since answers as a route that is not there.
    /// </summary>
    private static IResult AnswerOf(HttpContext context, ReversalRequestDraft draft, ReversalResult result)
    {
        if (result is { Outcome: ReversalOutcome.Recorded, Id: { } id })
        {
            return PageRedirect.Of(context, $"{ScreenPaths.ReversalMineOf(id)}?{Sent}");
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

    /// <summary>The requests of the person, or those of the others that wait for a decision, narrowed as the address says, one page of them.</summary>
    private static Task<IResult> ListAsync(HttpContext context, ReversalDesk? desk, bool mine) => ActingAsync(context, desk, async acting =>
    {
        var query = context.Request.Query;
        var (item, situation) = (query[ReversalListScreen.ItemKey].ToString(), query[ReversalListScreen.StateKey].ToString());
        var number = int.TryParse(query[ReversalListScreen.PageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) && asked > 0 ? asked : 1;
        var listing = new ReversalListing
        {
            DomainId = acting.Domain.Id,
            PersonId = acting.Person,
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
    private static Task<IResult> FollowAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request || request.RequestedBy != acting.Person)
        {
            return ScreenResult.NotFound(context);
        }

        var sent = context.Request.Query.ContainsKey(Sent) && request.State == ReversalState.Pending;
        return ScreenResult.Of(context, typeof(ReversalScreen), ReversalScreen.TitleOf(id, sent), new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalScreen.Request)] = request,
            [nameof(ReversalScreen.Domain)] = acting.Domain.Name,
            [nameof(ReversalScreen.Sent)] = sent,
        });
    });

    /// <summary>What the requests found missing, and the item or the marker left blank, which they take for the whole domain only once the rest is there.</summary>
    private static Dictionary<ReversalRequestField, Problem> MissingIn(ReversalRequestDraft draft, IEnumerable<ReversalRequestField> missing) =>
        missing.Concat(new[] { (Field: ReversalRequestField.Item, Typed: draft.Item), (Field: ReversalRequestField.Marker, Typed: draft.Marker) }.Where(typed => Blank(typed.Typed))
            .Select(typed => typed.Field)).Distinct().ToDictionary(field => field, _ => Problem.Missing);

    /// <summary>The field that made the request reach too far: more than one item or marker, or the one left blank, which reaches the whole domain.</summary>
    private static Dictionary<ReversalRequestField, Problem> WideIn(ReversalRequestDraft draft, ReversalScope? scope) => scope switch
    {
        ReversalScope.ManyItems => new() { [ReversalRequestField.Item] = Problem.ManyItems },
        ReversalScope.ManyMarkers => new() { [ReversalRequestField.Marker] = Problem.ManyMarkers },
        ReversalScope.Domain => new() { [Blank(draft.Item) ? ReversalRequestField.Item : ReversalRequestField.Marker] = Problem.WholeDomain },
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

    /// <summary>What a step acts over: the desk, the requests opened with the key read now, the domain where the person of the session acts, and that person.</summary>
    internal sealed record Acting(ReversalDesk Desk, ReversalAccess Access, EmptyingScreensEndpoint.ActiveDomain Domain, long Person);
}
