using System.Diagnostics.CodeAnalysis;
using Microsoft.Playwright;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// What every test of the run shares: the host on a real Kestrel and the Chromium that the job installed at the version the package pins.
/// A missing browser fails here, loudly, and never skips: the job exists to prove the screens in a browser.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the assembly fixture and the public test classes take it in their constructors.")]
public sealed class BrowserFixture : IAsyncLifetime
{
    private TrialScreenHost? host;
    private IPlaywright? playwright;
    private IBrowser? browser;

    public async ValueTask InitializeAsync()
    {
        host = await TrialScreenHost.StartAsync(CancellationToken.None);
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>A page, in a context of its own, already on the route given.</summary>
    internal Task<BrowserTab> OpenAsync(string route) =>
        OpenAtAsync((host ?? throw new InvalidOperationException("The host was not started.")).Address, route);

    /// <summary>A page, in a context of its own, already on the route of the host at the address given (a host a test class started for itself).</summary>
    internal async Task<BrowserTab> OpenAtAsync(Uri address, string route)
    {
        var tab = await BrowserTab.OpenAsync(browser ?? throw new InvalidOperationException("The browser was not started."), address);
        try
        {
            _ = await tab.Page.GotoAsync(route);
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
