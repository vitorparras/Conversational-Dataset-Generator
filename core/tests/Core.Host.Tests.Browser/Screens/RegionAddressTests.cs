using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The address of a screen once a region is swapped (issue 671, CA-3, decision 0051): a swap by a link or by a form of method GET that changes the page or the filter puts the state
/// in the address, going back and forward in the browser bring the region of that state back into the same document, a page opened by an address with the state shows it, and the
/// post of a form that swaps a region leaves the address as it was.
/// </summary>
[Trait("Category", "Browser")]
public sealed class RegionAddressTests(BrowserFixture browser)
{
    private const string SameDocument = "() => window.sameDocument === true";
    private const string FocusInRegion = "() => document.getElementById('lista').contains(document.activeElement)";

    [Fact]
    public async Task Ca3TheSwapByLinkPutsThePageInTheAddressAndBackAndForwardBringTheRegionOfThatPageIntoTheSameDocument()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.List);
        await MarkTheDocumentAsync(tab.Page);

        await tab.Page.Locator("#proxima").ClickAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(@"\?pagina=2"));
        await Expect(tab.Page.Locator("[data-page]")).ToHaveTextAsync("Página 2");
        await tab.Page.Locator("#proxima").ClickAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(@"\?pagina=3"));
        await Expect(tab.Page.Locator("[data-page]")).ToHaveTextAsync("Página 3");

        _ = await tab.Page.GoBackAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(@"\?pagina=2"));
        await Expect(tab.Page.Locator("[data-page]")).ToHaveTextAsync("Página 2");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusInRegion), "Going back left the focus outside the region that was brought back.");
        _ = await tab.Page.GoBackAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(TrialRoutes.List));
        await Expect(tab.Page.Locator("[data-page]")).ToHaveTextAsync("Página 1");
        _ = await tab.Page.GoForwardAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(@"\?pagina=2"));
        await Expect(tab.Page.Locator("[data-page]")).ToHaveTextAsync("Página 2");

        Assert.True(await tab.Page.EvaluateAsync<bool>(SameDocument), "The page was loaded again instead of the region being swapped.");
    }

    [Fact]
    public async Task Ca3TheSwapByAFormOfMethodGetPutsTheFilterInTheAddressAndBackBringsTheRegionOfBeforeIntoTheSameDocument()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.List);
        await MarkTheDocumentAsync(tab.Page);

        await tab.Page.Locator("#filtro").FillAsync("ZQX-filtro");
        await tab.Page.Locator("#filtrar").ClickAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(@"\?filtro=ZQX-filtro"));
        await Expect(tab.Page.Locator("[data-filter]")).ToHaveTextAsync("Filtro: ZQX-filtro");

        _ = await tab.Page.GoBackAsync();
        await Expect(tab.Page).ToHaveURLAsync(Ending(TrialRoutes.List));
        await Expect(tab.Page.Locator("[data-filter]")).ToHaveTextAsync("Filtro: nenhum");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusInRegion), "Going back left the focus outside the region that was brought back.");
        Assert.True(await tab.Page.EvaluateAsync<bool>(SameDocument), "The page was loaded again instead of the region being swapped.");
    }

    [Fact]
    public async Task Ca3AnAddressThatCarriesThePageAndTheFilterOpensTheListAsItSays()
    {
        await using var tab = await browser.OpenAsync($"{TrialRoutes.List}?pagina=3&filtro=ZQX-compartilhado");

        await Expect(tab.Page.Locator("[data-page]")).ToHaveTextAsync("Página 3");
        await Expect(tab.Page.Locator("[data-filter]")).ToHaveTextAsync("Filtro: ZQX-compartilhado");
        await Expect(tab.Page.Locator("#proxima")).ToHaveAttributeAsync("href", "?pagina=4&filtro=ZQX-compartilhado");
    }

    [Fact]
    public async Task Ca3ThePostOfAFormThatSwapsARegionLeavesTheAddressAsItWasAndAddsNothingToTheHistory()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        await MarkTheDocumentAsync(tab.Page);
        var history = await tab.Page.EvaluateAsync<int>("() => history.length");

        await tab.Page.Locator("#nome").FillAsync("operador-07");
        await tab.Page.Locator("#enviar").ClickAsync();

        await Expect(tab.Page.Locator("[data-saved]")).ToHaveTextAsync("Nome aceito: operador-07.");
        await Expect(tab.Page).ToHaveURLAsync(Ending(TrialRoutes.Trial));
        Assert.Equal(history, await tab.Page.EvaluateAsync<int>("() => history.length"));
        Assert.True(await tab.Page.EvaluateAsync<bool>(SameDocument), "The page was loaded again instead of the region being swapped.");
    }

    private static async Task MarkTheDocumentAsync(IPage page) => _ = await page.EvaluateAsync("() => { window.sameDocument = true; }");

    private static Regex Ending(string end) => new(end + "$", RegexOptions.None, TimeSpan.FromSeconds(1));
}
