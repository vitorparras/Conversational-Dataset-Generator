using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Endpoints;

/// <summary>
/// The screen of the seal of the manual situation (#736), for the technical administration alone: the seal as the chain stands is issued, to be kept outside the base, and the seal that was
/// kept is recorded, both through <see cref="SealState" />, the same operations the routes of <c>/custody/seal</c> use, so there is no path of its own to write. The seal goes in the body of
/// the post and in the answer, never in an address or in the log; a record done takes the browser back to the screen with 303 (decision 0051).
/// </summary>
internal static class SealScreensEndpoint
{
    /// <summary>The mark in the address of the screen that the seal kept has just been recorded.</summary>
    private const string RecordedMark = "registrado";

    /// <summary>What reaches the screen: the technical administration, as the seal is operated by who keeps the base.</summary>
    internal static ScreenAccess Access { get; } = ScreenAccess.Unlisted(PersonFunctions.Administration);

    /// <summary>Whether a screen offers the way to the seal: the installation is in the manual situation and whoever entered reaches the screen of the seal.</summary>
    internal static bool Offered(HttpContext context, SealDeclaration declared) =>
        declared.Situation == SealSituation.Manual && context.Features.Get<ScreenReach>() is { } reach && reach.Reaches(Access);

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Seal, ShowAsync).WithMetadata(Access);
        _ = endpoints.MapPost(ScreenPaths.SealIssue, IssueAsync).WithMetadata(Access);
        _ = endpoints.MapPost(ScreenPaths.SealConfirm, ConfirmAsync).WithMetadata(Access);
    }

    private static Task<RazorComponentResult<ScreenFrame>> ShowAsync(HttpContext context, [AsParameters] ChainSources sources) =>
        PageAsync(context, sources, new SealShown { Recorded = context.Request.Query.ContainsKey(RecordedMark) });

    /// <summary>The seal of the chain as it stands, written on the page and nowhere else; nothing is recorded, and the page is not kept.</summary>
    private static async Task<RazorComponentResult<ScreenFrame>> IssueAsync(HttpContext context, [AsParameters] ChainSources sources)
    {
        if (SealState.Availability(sources) != SealAvailability.Open)
        {
            return await PageAsync(context, sources, new SealShown());
        }

        var made = await SealState.IssueAsync(sources, context.RequestAborted);
        return await PageAsync(context, sources, made is null ? new SealShown { Unreadable = true } : new SealShown { Issued = made });
    }

    /// <summary>A seal recorded takes the browser to the screen, which says so; one refused comes back with the reason beside the field, and what was typed is given back.</summary>
    private static async Task<IResult> ConfirmAsync(HttpContext context, [AsParameters] ChainSources sources)
    {
        if (SealState.Availability(sources) != SealAvailability.Open)
        {
            return await PageAsync(context, sources, new SealShown());
        }

        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var typed = form is null ? string.Empty : form[SealScreen.KeptField].ToString();
        var confirmation = await SealState.ConfirmAsync(sources, SessionCookie.Read(context.Request), typed, context.RequestAborted);
        return confirmation.Outcome switch
        {
            SealConfirmationOutcome.Recorded => PageRedirect.Of(context, $"{ScreenPaths.Seal}?{RecordedMark}"),
            SealConfirmationOutcome.NoSession => ScreenResult.Unavailable(context),
            SealConfirmationOutcome.Unreadable => await PageAsync(context, sources, new SealShown { Unreadable = true, Typed = typed }),
            SealConfirmationOutcome.NotASeal or SealConfirmationOutcome.NotRecorded => await PageAsync(context, sources, new SealShown { Typed = typed, Refusal = confirmation.Reason }),
            _ => throw new ArgumentOutOfRangeException(nameof(context), confirmation.Outcome, "The record ended in a way the screen does not answer."),
        };
    }

    /// <summary>The page with what the chain holds of its seal; one that cannot be read now is the page that says so, with 503, and offers no action.</summary>
    private static async Task<RazorComponentResult<ScreenFrame>> PageAsync(HttpContext context, ChainSources sources, SealShown shown)
    {
        var open = SealState.Availability(sources) == SealAvailability.Open;
        var record = open ? await SealState.OnRecordAsync(sources, context.RequestAborted) : null;
        var unreadable = shown.Unreadable || (open && record is null) || SealState.Availability(sources) == SealAvailability.Unreadable;
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SealScreen.Manual)] = sources.Declared.Situation == SealSituation.Manual,
            [nameof(SealScreen.Unreadable)] = unreadable,
            [nameof(SealScreen.LastSeal)] = record?.LastAt,
            [nameof(SealScreen.Warning)] = record?.Warning,
            [nameof(SealScreen.Issued)] = shown.Issued,
            [nameof(SealScreen.Typed)] = shown.Typed,
            [nameof(SealScreen.Refusal)] = shown.Refusal,
            [nameof(SealScreen.Recorded)] = shown.Recorded,
        };
        var page = ScreenResult.Of(context, typeof(SealScreen), SealScreen.Title, screen);
        page.StatusCode = unreadable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;
        return page;
    }
}
