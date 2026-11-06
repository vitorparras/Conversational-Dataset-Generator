using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace Core.Tests.Browser.Accessibility;

/// <summary>
/// One setting of the person's system that the screens answer to: the colour scheme, the contrast and the transparency. The browser is told by the protocol,
/// which reaches the preferences that the emulation of the library does not (the reduced transparency).
/// </summary>
internal sealed record MediaMode(string Name, string Scheme, string Contrast, string Transparency, string Slug)
{
    public static readonly MediaMode Light = new("Light", "light", "no-preference", "no-preference", "claro");
    public static readonly MediaMode Dark = new("Dark", "dark", "no-preference", "no-preference", "escuro");
    public static readonly MediaMode ReducedTransparency = new("ReducedTransparency", "light", "no-preference", "reduce", "transparencia-reduzida");
    public static readonly MediaMode HighContrast = new("HighContrast", "light", "more", "no-preference", "contraste-alto");

    private static readonly ConditionalWeakTable<IPage, ICDPSession> Sessions = [];

    private static readonly MediaMode[] All = [Light, Dark, ReducedTransparency, HighContrast];

    /// <summary>The setting with the name given; the names are what a test lists in its data, since a public test cannot take the type.</summary>
    public static MediaMode Named(string name) => Array.Find(All, mode => mode.Name == name) ?? throw new ArgumentException($"No such setting: {name}.", nameof(name));

    /// <summary>Sets the page to this setting. The session of the protocol stays open with the context, because the override goes when it is detached.</summary>
    public async Task ApplyAsync(IPage page)
    {
        var session = Sessions.TryGetValue(page, out var open) ? open : await page.Context.NewCDPSessionAsync(page);
        Sessions.AddOrUpdate(page, session);
        var features = new[]
        {
            Feature("prefers-color-scheme", Scheme),
            Feature("prefers-contrast", Contrast),
            Feature("prefers-reduced-transparency", Transparency),
        };
        _ = await session.SendAsync("Emulation.setEmulatedMedia", new Dictionary<string, object> { ["features"] = features });
    }

    private static Dictionary<string, string> Feature(string name, string value) => new() { ["name"] = name, ["value"] = value };
}
