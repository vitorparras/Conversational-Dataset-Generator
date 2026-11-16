using Core.Host.Screens;
using Core.Host.Submissions;
using Core.Persistence.Domains;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The page of the submission (#607), a shell over the service of the submissions as the route of JSON is: the form offers the domains granted, the formats the gate reads and the conditions the
/// installation declared; the sending is judged by the service alone. An accepted file leads to its open batch, a refused one shows the verdict with its code, a field at fault opens the form again
/// with what was typed, and nothing of the material, the name of the file or the origin is said back beyond what the person typed in the form.
/// </summary>
internal static class BatchSubmissionPage
{
    /// <summary>
    /// What the page lets through past the file and its margin, so a file a little too large reaches the gate, is refused with its code and is recorded as the route of JSON records it
    /// (risk of the phase 003, "Arquivo grande"); past it the server cuts the body and answers 413 alone.
    /// </summary>
    private const long Excess = 32L * 1024 * 1024;

    /// <summary>The form, or what keeps the ingestion closed; with no database declared the host cannot, and says so.</summary>
    internal static async Task<IResult> FormAsync(HttpContext context, [FromServices] SubmissionServiceHolder? services, [FromServices] DomainListing? listing)
    {
        if (services is null || listing is null)
        {
            return ScreenResult.Unavailable(context);
        }

        var granted = await GrantedDomains.OfScreenAsync(listing, context);
        using var loaded = await services.GetAsync(context.RequestAborted);
        if (loaded is null)
        {
            return ScreenResult.Unavailable(context);
        }

        var gate = await loaded.Service.GateAsync(context.RequestAborted);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SubmissionScreen.BlockedBy)] = gate.Open ? null : gate.Code };
        var offer = SubmissionPageView.OfferOf(granted, loaded.Service, services.MaxBytes);
        return SubmissionPageView.Page(context, offer, fields, gate.Open ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// The sending: the session, the body bounded before a byte is read, the domain among the granted ones, and then the reply. It starts as the route of JSON does, and answers as a screen does:
    /// the page of the host for what the screen cannot show. What the reply left unread is drained, so the browser reads the page instead of a connection cut.
    /// </summary>
    internal static async Task<IResult> SubmitAsync(HttpContext context, [FromServices] SubmissionServiceHolder? services, [FromServices] DomainListing? listing)
    {
        if (services is null || listing is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (RequestReader.SessionOf(context) is not { } author)
        {
            return Results.Unauthorized();
        }

        SubmissionsEndpoint.Bound(context, services.MaxBytes, Excess);
        if (await SubmissionForm.ReadAsync(context.Request, author, context.RequestAborted) is not { } read)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var granted = await GrantedDomains.OfScreenAsync(listing, context);
        if (read.Request.DomainId > 0 && !granted.ContainsKey(read.Request.DomainId))
        {
            return ScreenResult.NotFound(context);
        }

        using var loaded = await services.GetAsync(context.RequestAborted);
        var reply = loaded is null
            ? ScreenResult.Unavailable(context)
            : await BatchSubmissionReply.SendAsync(context, (loaded.Service, SubmissionPageView.OfferOf(granted, loaded.Service, services.MaxBytes)), read);
        await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted);
        return reply;
    }
}
