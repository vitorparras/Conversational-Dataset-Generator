using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>What the tests of the screens of the people (issue 599) share: the routes, the way into a screen as a person of the run, and the reading of the list and of the pager.</summary>
internal static class PeoplePages
{
    public const string List = "/pessoas";
    public const string Form = "/pessoas/nova";
    public const string Missing = "/zqx-nao-existe-5990";
    public const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    public const string Focused = "() => document.activeElement.id || document.activeElement.textContent.trim()";
    public const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    public const int PhoneWidth = 390;
    public const int PhoneHeight = 844;
    public const int StatusNotFound = 404;

    private const string ReadRows =
        "(rows) => rows.map((row) => [row.querySelector('strong').textContent.trim(), row.children[1].textContent.trim(), row.querySelector('.mark').lastChild.textContent.trim()])";

    /// <summary>The people the list shows, each as its name, its function and its state, read from the rows of the page.</summary>
    public static async Task<IReadOnlyList<string[]>> RowsAsync(IPage page) => await page.Locator(".paged-list li .row").EvaluateAllAsync<string[][]>(ReadRows);

    /// <summary>The people of <see cref="PeopleScreenHost.Listed"/> the name given is part of, as the rows the list shows them as.</summary>
    public static IEnumerable<string[]> ListedWith(string part) =>
        PeopleScreenHost.Listed.Where(seed => seed.Name.Contains(part, StringComparison.OrdinalIgnoreCase)).Select(Row);

    public static string[] Row(PeopleScreenHost.Seed seed) => [seed.Name, seed.Function, seed.Active ? "Ativa" : "Inativa"];

    /// <summary>A page, in a context of its own, of a person who entered by the form and opened the route given.</summary>
    public static async Task<BrowserTab> OpenAsync(BrowserFixture browser, ScreenHostBase host, string person, string route)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, person);
        try
        {
            _ = await tab.Page.GotoAsync(route);
            await Expect(tab.Page.Locator("h1")).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }

    public static ILocator Pager(IPage page) => page.Locator("nav.pager [aria-current=page]");

    public static string Made(string kind) => $"zqx-{kind}-{Guid.NewGuid():N}";
}
