using Core.Host.Screens;
using Core.Persistence.Content.Rerun;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the rerun of the sanitization (issue 97), reached only by the curation of the collection with a domain granted: the items by version, the confirmation on a page of its own, whose
/// POST only queues and ends in a 303 to the progress, and the progress of a request, which a link reloads. The worker treats the queue; nothing here treats an item or reads a text.
/// </summary>
internal static class RerunScreensEndpoint
{
    private const int Place = 22;

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Rerun, ShowAsync).WithMetadata(ScreenAccess.Of(RerunScreen.Title, Place, PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.RerunConfirm, ConfirmAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapPost(ScreenPaths.RerunConfirm, RequestAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.RerunProgress, FollowAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
    }

    private static async Task<IResult> ShowAsync(HttpContext context, [FromServices] RerunDesk? desk)
    {
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        return await ActiveAsync(context, desk) is { } domain
            ? ScreenResult.Of(context, typeof(RerunScreen), RerunScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(RerunScreen.Domain)] = domain.Name,
                [nameof(RerunScreen.Current)] = desk.Version,
                [nameof(RerunScreen.Situation)] = await desk.SituationAsync(domain.Id, context.RequestAborted),
            })
            : ScreenResult.NotFound(context);
    }

    /// <summary>The confirmation, or the situation when no item is in another version.</summary>
    private static async Task<IResult> ConfirmAsync(HttpContext context, [FromServices] RerunDesk? desk)
    {
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await ActiveAsync(context, desk) is not { } domain)
        {
            return ScreenResult.NotFound(context);
        }

        var situation = await desk.SituationAsync(domain.Id, context.RequestAborted);
        return RerunScreen.Behind(situation, desk.Version) == 0 ? PageRedirect.Of(context, ScreenPaths.Rerun) : Confirmation(context, (domain, desk.Version), situation);
    }

    /// <summary>Queues the items in the name of the person of the session and leads to the request, new or open; a request the chain did not take comes back on the confirmation.</summary>
    private static async Task<IResult> RequestAsync(HttpContext context, [FromServices] RerunDesk? desk)
    {
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        var domain = await ActiveAsync(context, desk);
        if (domain is null || RequestReader.SessionOf(context) is not { } entered)
        {
            return ScreenResult.NotFound(context);
        }

        var result = await desk.RequestAsync(domain.Id, entered, context.RequestAborted);
        if (result is { Outcome: RerunOutcome.Queued or RerunOutcome.AlreadyOpen, RerunId: { } rerun })
        {
            return PageRedirect.Of(context, ScreenPaths.RerunProgressOf(rerun));
        }

        if (result.Outcome == RerunOutcome.NothingToRerun)
        {
            return PageRedirect.Of(context, ScreenPaths.Rerun);
        }

        return result.Outcome == RerunOutcome.NotRecorded
            ? Confirmation(context, (domain, desk.Version), await desk.SituationAsync(domain.Id, context.RequestAborted), StatusCodes.Status503ServiceUnavailable)
            : throw new ArgumentOutOfRangeException(nameof(context), result.Outcome, "The request ended in a way the screen does not answer.");
    }

    private static async Task<IResult> FollowAsync(HttpContext context, long id, [FromServices] RerunDesk? desk)
    {
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        return await ActiveAsync(context, desk) is { } domain && await desk.ProgressAsync(domain.Id, id, context.RequestAborted) is { } followed
            ? ScreenResult.Of(context, typeof(RerunProgressScreen), RerunProgressScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(RerunProgressScreen.Id)] = id,
                [nameof(RerunProgressScreen.Current)] = desk.Version,
                [nameof(RerunProgressScreen.Progress)] = followed.Progress,
                [nameof(RerunProgressScreen.Shown)] = followed.Phase,
            })
            : ScreenResult.NotFound(context);
    }

    /// <summary>The active domain of whoever entered, the first granted until #112, with its identifier; none when no domain is granted or the name is of no domain.</summary>
    private static async Task<EmptyingScreensEndpoint.ActiveDomain?> ActiveAsync(HttpContext context, RerunDesk desk)
    {
        var granted = context.Features.Get<ScreenReach>()?.Granted.FirstOrDefault();
        return granted is not null && await desk.DomainIdOfAsync(granted, context.RequestAborted) is { } id ? new EmptyingScreensEndpoint.ActiveDomain(granted, id) : null;
    }

    private static RazorComponentResult<ScreenFrame> Confirmation(
        HttpContext context, (EmptyingScreensEndpoint.ActiveDomain Domain, string Current) at, RerunSituation situation, int status = StatusCodes.Status200OK)
    {
        var page = ScreenResult.Of(context, typeof(RerunConfirmScreen), RerunConfirmScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(RerunConfirmScreen.Domain)] = at.Domain.Name,
            [nameof(RerunConfirmScreen.Current)] = at.Current,
            [nameof(RerunConfirmScreen.Situation)] = situation,
            [nameof(RerunConfirmScreen.Shown)] = status == StatusCodes.Status200OK ? RerunConfirmScreen.Problem.None : RerunConfirmScreen.Problem.NotWritten,
        });
        page.StatusCode = status;
        return page;
    }
}
