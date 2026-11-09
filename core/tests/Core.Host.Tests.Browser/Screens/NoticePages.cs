using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>What the tests of the notices (issue 595) share: the ways into the list and the screen of the domains, the act that sets a password again, and the words the screens say.</summary>
internal static class NoticePages
{
    public const string List = "/avisos";
    public const string DomainsRoute = "/dominios";
    public const string Missing = "/zqx-nao-existe-5950";
    public const string Rows = "[aria-labelledby=avisos-titulo] tbody tr";
    public const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    public const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";
    public const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    public const int PhoneWidth = 390;
    public const int PhoneHeight = 844;
    public const int StatusNotFound = 404;
    public const string ResetWords = "Sua senha foi reposta por quem administra o sistema. ";
    public const string SuspendedWords =
        "A escrita nos domínios foi suspensa porque a cadeia de custódia não conferiu na última verificação. A leitura continua.";
    public const string SuspendedBanner = "A escrita está suspensa nos domínios: a última verificação da cadeia de custódia não conferiu. A leitura continua.";

    /// <summary>The list of the notices, open for the person who entered.</summary>
    public static Task<BrowserTab> OpenListAsync(BrowserFixture browser, ScreenHostBase host, string name) =>
        OpenAsync(browser, host, name, List, "Avisos");

    /// <summary>The screen of the domains, open for the person who entered.</summary>
    public static Task<BrowserTab> OpenDomainsAsync(BrowserFixture browser, ScreenHostBase host, string name) =>
        OpenAsync(browser, host, name, DomainsRoute, "Domínios");

    /// <summary>Enters with the name and the password given, in a context of its own, as the person does after a password set again.</summary>
    public static async Task<BrowserTab> EnterAsync(BrowserFixture browser, ScreenHostBase host, string name, string password)
    {
        var tab = await browser.OpenAtAsync(host.Address, "/entrada");
        try
        {
            await tab.Page.Locator("#entrada-nome").FillAsync(name);
            await tab.Page.Locator("#entrada-senha").FillAsync(password);
            await tab.Page.Locator("#entrar").ClickAsync();
            await Expect(tab.Page.Locator("#sair")).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }

    /// <summary>Sets the password of the person again by the route of the administration, in the session of the tab, which must be one of an administrator.</summary>
    public static async Task ResetPasswordAsync(BrowserTab administrator, long personId, string password)
    {
        var address = new Uri(new Uri(administrator.Page.Url), $"/people/{personId}/password").AbsoluteUri;
        var answer = await administrator.Page.Context.APIRequest.PutAsync(address, new() { DataObject = new { password } });
        Assert.Equal(200, answer.Status);
    }

    /// <summary>The words a row of the list says of its notice, without the button.</summary>
    public static ILocator WordsOfRows(IPage page) => page.Locator($"{Rows} th[scope=row]");

    private static async Task<BrowserTab> OpenAsync(BrowserFixture browser, ScreenHostBase host, string name, string route, string title)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, name);
        try
        {
            _ = await tab.Page.GotoAsync(route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = title, Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
