using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The pages of the list of the people (issue 599, CA-2 and decision 0051) in a real Chromium: twenty to a page, the size said on the page, the page and the filter in the address, so that
/// going back, reloading and sharing the address give the same page; a page that does not exist in the address is the nearest one that does, and the filter stays in force from page to page.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeoplePagingTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private const string Listed = "/pessoas?nome=zqx-lista-";
    private const int Size = 20;

    private static IEnumerable<string[]> PageOf(int page, string state = "") =>
        ListedWith("zqx-lista-").Where(row => state.Length == 0 || row[2] == state).Skip((page - 1) * Size).Take(Size);

    [Fact]
    public async Task TheFirstPageHasTwentyPeopleSaysItsSizeAndOffersOnlyTheNextOne()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Listed);

        Assert.Equal(PageOf(1), await RowsAsync(tab.Page));
        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 1 de 3, 20 por página");
        await Expect(tab.Page.Locator("nav.pager a[rel=prev]")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator("nav.pager a[rel=next]")).ToHaveAttributeAsync("href", "?nome=zqx-lista-&pagina=2");
        await Expect(tab.Page.Locator("nav.pager")).ToHaveAttributeAsync("aria-label", "Páginas de pessoas");
    }

    [Fact]
    public async Task TheNextPageAndThePreviousOneChangeTheAddressAndTheLastPageHoldsWhatIsLeft()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Listed);

        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Próxima" }).ClickAsync();
        await Expect(tab.Page).ToHaveURLAsync(new Regex("[?&]pagina=2$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Equal(PageOf(2), await RowsAsync(tab.Page));
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Próxima" }).ClickAsync();
        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 3 de 3, 20 por página");
        Assert.Equal(PageOf(3), await RowsAsync(tab.Page));
        await Expect(tab.Page.Locator("nav.pager a[rel=next]")).ToHaveCountAsync(0);
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Anterior" }).ClickAsync();

        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 2 de 3, 20 por página");
        Assert.Equal(PageOf(2), await RowsAsync(tab.Page));
    }

    [Fact]
    public async Task GoingBackAndReloadingGiveTheSamePageBecauseThePageIsInTheAddress()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Listed);
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Próxima" }).ClickAsync();
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Próxima" }).ClickAsync();
        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 3 de 3, 20 por página");

        _ = await tab.Page.GoBackAsync();
        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 2 de 3, 20 por página");
        Assert.Equal(PageOf(2), await RowsAsync(tab.Page));
        _ = await tab.Page.ReloadAsync();
        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 2 de 3, 20 por página");
        _ = await tab.Page.GoBackAsync();

        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 1 de 3, 20 por página");
        Assert.Equal(PageOf(1), await RowsAsync(tab.Page));
    }

    [Fact]
    public async Task TheAddressOfAPageOpensTheSamePageForAnotherPersonOfTheAdministration()
    {
        await using var one = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{Listed}&pagina=3");
        var address = one.Page.Url;
        await using var other = await OpenAsync(browser, people.Host, people.Host.Administrator, address);

        Assert.Equal(await RowsAsync(one.Page), await RowsAsync(other.Page));
        Assert.Equal(PageOf(3), await RowsAsync(other.Page));
    }

    [Theory]
    [InlineData("99", 3)]
    [InlineData("0", 1)]
    [InlineData("-1", 1)]
    [InlineData("abc", 1)]
    [InlineData("", 1)]
    public async Task APageThatDoesNotExistInTheAddressIsTheLastOneOrTheFirstAndNeverAnError(string asked, int shown)
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{Listed}&pagina={asked}");

        await Expect(Pager(tab.Page)).ToHaveTextAsync($"Página {shown} de 3, 20 por página");
        Assert.Equal(PageOf(shown), await RowsAsync(tab.Page));
    }

    [Fact]
    public async Task TheFilterStaysInForceFromPageToPageInTheLinksAndInTheAddress()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{Listed}&estado=ativas");

        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 1 de 3, 20 por página");
        await Expect(tab.Page.Locator("nav.pager a[rel=next]")).ToHaveAttributeAsync("href", "?nome=zqx-lista-&estado=ativas&pagina=2");
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Próxima" }).ClickAsync();
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Próxima" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex("estado=ativas&pagina=3$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Equal(PageOf(3, "Ativa"), await RowsAsync(tab.Page));
        await Expect(tab.Page.Locator("#filtro-estado")).ToHaveValueAsync("ativas");
        await Expect(tab.Page.Locator("#filtro-nome")).ToHaveValueAsync("zqx-lista-");
    }

    [Fact]
    public async Task AFilterThatLeavesOnePageShowsNoPagerAndNoWayThatLeadsNowhere()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{Listed}&estado=inativas");

        await Expect(tab.Page.Locator("ul.paged-list li").First).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("nav.pager")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByText("Anterior", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByText("Próxima", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheFirstAndTheLastPageShowNoWayBeyondThemNotEvenAsText()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Listed);
        await Expect(tab.Page.Locator("nav.pager").GetByText("Anterior")).ToHaveCountAsync(0);

        _ = await tab.Page.GotoAsync($"{Listed}&pagina=3");

        await Expect(Pager(tab.Page)).ToHaveTextAsync("Página 3 de 3, 20 por página");
        await Expect(tab.Page.Locator("nav.pager").GetByText("Próxima")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator("nav.pager a[rel=prev]")).ToHaveCountAsync(1);
    }
}
