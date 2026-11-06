using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Screens;

/// <summary>What a route returns for a screen: the whole page, or only the region the client layer asked for by the header it sends.</summary>
internal static class ScreenResult
{
    /// <summary>The header the client layer sets on a swap; its value is the identifier of the region it will replace.</summary>
    internal const string RegionHeader = "X-Region";

    /// <summary>The screen of the type given, a component, with the title of its page and what it takes.</summary>
    internal static RazorComponentResult<ScreenFrame> Of(HttpContext context, Type screen, string title, IDictionary<string, object?>? parameters = null) =>
        new(new { Title = title, Screen = screen, ScreenParameters = parameters, Asked = Asked(context.Request) });

    private static string? Asked(HttpRequest request) =>
        request.Headers.TryGetValue(RegionHeader, out var asked) && asked.Count == 1 && !string.IsNullOrEmpty(asked[0]) ? asked[0] : null;
}
