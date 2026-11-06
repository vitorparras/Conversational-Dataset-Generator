using System.Collections.Concurrent;
using Core.Tests.Browser.Fixtures;
using Core.Tests.Browser.Trial;

namespace Core.Tests.Browser.Screens;

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
        await using var tab = await browser.OpenAsync(address);
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

        Assert.Equal((1, 1), (TrialRoutes.Received(TrialRoutes.KeyOf(mode)), posts.Count));
    }
}
