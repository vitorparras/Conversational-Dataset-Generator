using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Screens;

/// <summary>What a route returns for a screen: the whole page, with the screen inside the base page.</summary>
internal static class ScreenResult
{
    /// <summary>The screen of the type given, a component, with the title of its page and what it takes.</summary>
    internal static RazorComponentResult<ScreenFrame> Of(Type screen, string title, IDictionary<string, object?>? parameters = null) =>
        new(new { Title = title, Screen = screen, ScreenParameters = parameters });
}
