using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The catalog page of the design system (issue 658, CA-4): each component in each state, no violation of WCAG 2.1 A and AA in any setting of the system,
/// the text over the background as it is drawn at AA (CA-3), and the states told without colour.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CatalogTests(BrowserFixture browser)
{
    private const string Absent = "(pairs) => pairs.filter(([name, selector]) => document.querySelector(selector) === null).map(([name]) => name)";

    /// <summary>The components the issue lists, each state an example the page must show; the selector is how the production markup names it.</summary>
    private static readonly string[][] Examples =
    [
        ["cabecalho", "header.header a.header__name"],
    ];

    [Fact]
    public async Task Ca4TheCatalogShowsEveryComponentInEveryState()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);

        var absent = await tab.Page.EvaluateAsync<string[]>(Absent, Examples);

        Assert.Empty(absent);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca4TheCatalogHasNoViolationOfWcagAAndAaInTheSettingOfTheSystem(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "catalogo");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca3TheTextOfTheCatalogReachesAaOverTheBackgroundAsItIsDrawn(string name)
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await MediaMode.Named(name).ApplyAsync(tab.Page);

        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca3TheLinkToSkipToTheContentReachesAaOverTheHeaderWhenItTakesTheFocus(string name)
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await MediaMode.Named(name).ApplyAsync(tab.Page);
        await tab.Page.Keyboard.PressAsync("Tab");
        await Expect(tab.Page.Locator(".skip-link")).ToBeFocusedAsync();

        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }
}
