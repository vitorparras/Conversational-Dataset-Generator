using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the chain (#605), which show what the route of JSON reads, from the same call, to the technical administration and the audit. A verification asked for now
/// is confirmed on a page of its own (decision 0051), posted from it, and recorded with who asked; nothing here rebuilds a link.
/// </summary>
internal static class ChainScreensEndpoint
{
    private const int ChainPlace = 93;

    /// <summary>The mark in the address of the chain that a verification asked for has just finished.</summary>
    private const string AskedMark = "pedida";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var chain = new[] { PersonFunctions.Administration, PersonFunctions.Audit };
        _ = endpoints.MapGet(ScreenPaths.Chain, ChainAsync).WithMetadata(ScreenAccess.Of(ChainScreen.Title, ChainPlace, chain));
        _ = endpoints.MapGet(ScreenPaths.ChainCheck, Confirm).WithMetadata(ScreenAccess.Unlisted(chain));
        _ = endpoints.MapPost(ScreenPaths.ChainCheck, CheckAsync).WithMetadata(ScreenAccess.Unlisted(chain));
    }

    /// <summary>A chain that cannot be read now is a page that says so, with 503, and shows nothing of a verification.</summary>
    private static async Task<RazorComponentResult<ScreenFrame>> ChainAsync(HttpContext context, [AsParameters] ChainSources sources)
    {
        if (await ChainState.ReadAsync(sources, context.RequestAborted) is not { } shown)
        {
            var unreadable = ScreenResult.Of(context, typeof(ChainScreen), ChainScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(ChainScreen.Unreadable)] = true });
            unreadable.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return unreadable;
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ChainScreen.Last)] = shown.Last,
            [nameof(ChainScreen.History)] = shown.History,
            [nameof(ChainScreen.Interval)] = shown.Interval,
            [nameof(ChainScreen.Overdue)] = shown.Overdue,
            [nameof(ChainScreen.Protection)] = shown.Protection,
            [nameof(ChainScreen.LastSeal)] = shown.LastSeal,
            [nameof(ChainScreen.SealWarning)] = shown.Warning,
            [nameof(ChainScreen.Asked)] = context.Request.Query.ContainsKey(AskedMark),
            [nameof(ChainScreen.SealAction)] = SealScreensEndpoint.Offered(context, sources.Declared),
        };
        return ScreenResult.Of(context, typeof(ChainScreen), ChainScreen.Title, screen);
    }

    private static RazorComponentResult<ScreenFrame> Confirm(HttpContext context) => ScreenResult.Of(context, typeof(ChainCheckScreen), ChainCheckScreen.Title);

    /// <summary>A verification done takes the browser to the chain, which shows it; one refused for coming too soon, or that did not reach its end, is the confirmation again saying so.</summary>
    private static async Task<IResult> CheckAsync(HttpContext context, [AsParameters] ChainSources sources)
    {
        var asked = await ChainState.VerifyAsync(sources, SessionCookie.Read(context.Request), seal: null, context.RequestAborted);
        if (asked.Done is not null)
        {
            return PageRedirect.Of(context, $"{ScreenPaths.Chain}?{AskedMark}");
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ChainCheckScreen.WaitSeconds)] = asked.WaitSeconds,
            [nameof(ChainCheckScreen.RetryAt)] = asked.WaitSeconds is { } wait ? RetryMinute(context, wait) : null,
            [nameof(ChainCheckScreen.Unfinished)] = asked.WaitSeconds is null,
        };
        return ScreenResult.Of(context, typeof(ChainCheckScreen), ChainCheckScreen.Title, screen);
    }

    /// <summary>The first whole minute at or after the end of the wait, which is the precision the page writes a time in, so that asking at it is never too soon.</summary>
    private static DateTimeOffset RetryMinute(HttpContext context, int waitSeconds)
    {
        var clock = context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        var end = clock.GetUtcNow().AddSeconds(waitSeconds);
        var minute = new DateTimeOffset(end.Year, end.Month, end.Day, end.Hour, end.Minute, 0, TimeSpan.Zero);
        return minute < end ? minute.AddMinutes(1) : minute;
    }
}
