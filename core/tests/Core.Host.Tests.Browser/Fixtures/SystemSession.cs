using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The way into the host of the system screens: the form of the entry, with the password of the run, in a context of its own.</summary>
internal static class SystemSession
{
    /// <summary>A page, in a context of its own, of a person who entered by the form and stands on the start page.</summary>
    public static Task<BrowserTab> SignedInAsync(BrowserFixture browser, SystemScreenHost host, string name)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(host);
        return EnterAsync(browser, host, name);
    }

    private static async Task<BrowserTab> EnterAsync(BrowserFixture browser, SystemScreenHost host, string name)
    {
        var tab = await browser.OpenAtAsync(host.Address, "/entrada");
        try
        {
            await tab.Page.Locator("#entrada-nome").FillAsync(name);
            await tab.Page.Locator("#entrada-senha").FillAsync(host.Password);
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
}
