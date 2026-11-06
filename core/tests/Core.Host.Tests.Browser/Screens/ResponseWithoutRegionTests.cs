using System.Collections.Concurrent;
using System.Globalization;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The post of a form that swaps a region, when the answer does not bring the region (review of the pull request 667): the server has already done what the post asks, so
/// the client never sends it again. A second send would do twice what must be done once, such as the entry of a person.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ResponseWithoutRegionTests(BrowserFixture browser)
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(750);

    [Theory]
    [InlineData("outra")]
    [InlineData("erro")]
    [InlineData("redireciona")]
    public Task TheFormWhoseAnswerBringsNoRegionReachesTheServerOnce(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        return ReachesOnceAsync(mode);
    }

    private async Task ReachesOnceAsync(string mode)
    {
        var address = $"{TrialRoutes.Submit}/{mode}";
        var key = $"ENVIO_{mode.ToUpperInvariant()}_{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}";
        await using var tab = await browser.OpenAsync($"{address}?key={key}");
        var posts = new ConcurrentQueue<string>();
        tab.Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.EndsWith(address, StringComparison.Ordinal))
            {
                posts.Enqueue(request.Url);
            }
        };

        _ = await tab.Page.RunAndWaitForResponseAsync(() => tab.Page.Locator("#enviar").ClickAsync(), response => response.Request.Method == "POST");
        await Task.Delay(Settle, TestContext.Current.CancellationToken);

        Assert.Equal((1, 1), (TrialRoutes.Received(key), posts.Count));
    }

    [Fact]
    public async Task TheAnswerThatIsAnotherScreenShowsAsAWholePageWithItsFrame()
    {
        await using var tab = await browser.OpenAsync($"{TrialRoutes.Submit}/outra");

        await tab.Page.Locator("#enviar").ClickAsync();

        await Expect(tab.Page.Locator("h1")).ToHaveTextAsync("Tela base");
        await Expect(tab.Page.Locator("html")).ToHaveAttributeAsync("lang", "pt-BR");
        Assert.Contains("Gerador de bases conversacionais", await tab.Page.TitleAsync(), StringComparison.Ordinal);
        Assert.True(await tab.Page.EvaluateAsync<bool>("() => document.styleSheets.length > 0"), "The page came without its style sheet.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
    }
}
