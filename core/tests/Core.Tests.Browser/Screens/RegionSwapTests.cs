using Core.Tests.Browser.Fixtures;
using Core.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Tests.Browser.Screens;

/// <summary>The swap of a region the markup declares (decision 0010): what is typed outside the region stays, the focus lands where the region says, and the page is not reloaded.</summary>
[Trait("Category", "Browser")]
public sealed class RegionSwapTests(BrowserFixture browser)
{
    private const string Typed = "texto em andamento";

    [Fact]
    public async Task Ca4TheSwapOfARegionKeepsWhatWasTypedInTheFormOutsideIt()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        await tab.Page.Locator("#nome").FillAsync(Typed);
        _ = await tab.Page.EvaluateAsync("() => { document.querySelector('form').dataset.mark = 'original'; }");

        await tab.Page.Locator("#acionar").ClickAsync();

        await Expect(tab.Page.Locator("[data-status]")).ToHaveTextAsync(TrialRoutes.ShortcutMessage);
        await Expect(tab.Page.Locator("#nome")).ToHaveValueAsync(Typed);
        await Expect(tab.Page.Locator("form")).ToHaveAttributeAsync("data-mark", "original");
    }

    [Fact]
    public async Task Ca6TheFocusLandsOnTheElementTheRegionDeclaresOnceItIsSwappedIn()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);

        await tab.Page.Locator("#acionar").ClickAsync();

        await Expect(tab.Page.Locator("[data-status]")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task TheFormSwapsItsRegionWithoutReloadingThePage()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        _ = await tab.Page.EvaluateAsync("() => { window.zqxMark = 'same page'; }");

        await tab.Page.Locator("#nome").FillAsync("operador-07");
        await tab.Page.Locator("#enviar").ClickAsync();

        await Expect(tab.Page.Locator("[data-saved]")).ToHaveTextAsync("Nome aceito: operador-07.");
        Assert.Equal("same page", await tab.Page.EvaluateAsync<string>("() => window.zqxMark"));
        Assert.EndsWith(TrialRoutes.Trial, tab.Page.Url, StringComparison.Ordinal);
    }
}
