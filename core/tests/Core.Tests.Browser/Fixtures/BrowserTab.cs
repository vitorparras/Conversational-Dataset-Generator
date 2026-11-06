using Microsoft.Playwright;

namespace Core.Tests.Browser.Fixtures;

/// <summary>One page of a context of its own, so that no test sees the cookies or the storage of another; closed with the object.</summary>
internal sealed class BrowserTab : IAsyncDisposable
{
    private readonly IBrowserContext context;

    private BrowserTab(IBrowserContext context, IPage page)
    {
        this.context = context;
        Page = page;
    }

    public IPage Page { get; }

    public static async Task<BrowserTab> OpenAsync(IBrowser browser, Uri address)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { Locale = "pt-BR", BaseURL = address.AbsoluteUri });
        try
        {
            return new BrowserTab(context, await context.NewPageAsync());
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
