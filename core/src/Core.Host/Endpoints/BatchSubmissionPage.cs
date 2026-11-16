using Core.Host.Screens;
using Core.Host.Submissions;
using Core.Persistence.Domains;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The page of the submission (#607), a shell over the service of the submissions as the route of JSON is: the form offers the domains granted, the formats the gate reads and the conditions the
/// installation declared; the sending is judged by the service alone. An accepted file leads to its open batch, a refused one shows the verdict with its code, a field at fault opens the form again
/// with what was typed, and nothing of the material, the name of the file or the origin is said back beyond what the person typed in the form.
/// </summary>
internal static class BatchSubmissionPage
{
    private const int BodyMargin = 64 * 1024;

    /// <summary>The form, or what keeps the ingestion closed; with no database declared the host cannot, and says so.</summary>
    internal static async Task<IResult> FormAsync(HttpContext context, [FromServices] SubmissionServiceHolder? services, [FromServices] DomainListing? listing)
    {
        if (services is null || listing is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var granted = await GrantedAsync(context, listing);
        using var loaded = await services.GetAsync(context.RequestAborted);
        if (loaded is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var gate = await loaded.Service.GateAsync(context.RequestAborted);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SubmissionScreen.BlockedBy)] = gate.Open ? null : gate.Code };
        var offer = SubmissionPageView.OfferOf(granted, loaded.Service, services.MaxBytes);
        return SubmissionPageView.Page(context, offer, fields, gate.Open ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>The sending: the session, the body bounded before a byte is read, the domain among the granted ones, the gate, the fields, and then the service.</summary>
    internal static async Task<IResult> SubmitAsync(HttpContext context, [FromServices] SubmissionServiceHolder? services, [FromServices] DomainListing? listing)
    {
        if (services is null || listing is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request)) is not { } author)
        {
            return Results.Unauthorized();
        }

        Bound(context, services.MaxBytes);
        if (await SubmissionForm.ReadAsync(context.Request, author, context.RequestAborted) is not { } read)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var granted = await GrantedAsync(context, listing);
        if (read.Request.DomainId > 0 && !granted.ContainsKey(read.Request.DomainId))
        {
            return Results.NotFound();
        }

        using var loaded = await services.GetAsync(context.RequestAborted);
        return loaded is null
            ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
            : await BatchSubmissionReply.SendAsync(context, (loaded.Service, SubmissionPageView.OfferOf(granted, loaded.Service, services.MaxBytes)), read);
    }

    /// <summary>The domains granted to the one who entered, by identifier, as the gate of the screens read them for the request.</summary>
    private static Task<Dictionary<int, string>> GrantedAsync(HttpContext context, DomainListing listing) =>
        GrantedDomains.OfAsync(listing, (context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing).Granted, context.RequestAborted);

    /// <summary>The body is bounded by the most bytes a file may have, and a margin for the fields and the boundaries, before a byte of it is read.</summary>
    private static void Bound(HttpContext context, long maxBytes)
    {
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
        {
            feature.MaxRequestBodySize = maxBytes + BodyMargin;
        }
    }
}
