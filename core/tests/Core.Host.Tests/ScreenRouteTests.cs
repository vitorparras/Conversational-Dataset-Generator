using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Tests;

/// <summary>
/// The screens as the host serves them (decision 0052), in process: the style is the tokens followed by the components, with no session and not sniffed,
/// and a screen comes inside the base page, in Portuguese, with its title, the link to the content and the header.
/// </summary>
public sealed class ScreenRouteTests : IAsyncLifetime
{
    private const string Page = "/zqx-tela-7731";
    private const string Title = "Tela zqx";

    private WebApplication? app;
    private HttpClient? client;

    private HttpClient Client => client ?? throw new InvalidOperationException("The host was not started.");

    public async ValueTask InitializeAsync()
    {
        var address = HostProcess.FreePlainAddress();
        var builder = CompositionRoot.ClosedBuilder(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        app = builder.Build();
        ScreenAssets.Map(app);
        _ = app.MapGet(Page, Show).WithMetadata(new SessionOpen("the test of the screens"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        client = new HttpClient { BaseAddress = address };
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheStyleIsTheTokensFollowedByTheComponentsWithNoSessionAndNotSniffed()
    {
        using var response = await Client.GetAsync(new Uri(ScreenAssets.StyleRoute, UriKind.Relative), TestContext.Current.CancellationToken);
        var style = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("text/css", "utf-8", "nosniff"), (response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentType?.CharSet, Sniffing(response)));
        Assert.InRange(style.IndexOf("--canvas:", StringComparison.Ordinal), 0, style.IndexOf(".header {", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AScreenComesInsideTheBasePageWithItsTitleTheLinkToTheContentAndTheHeader()
    {
        var page = await Client.GetStringAsync(new Uri(Page, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains("<html lang=\"pt-BR\">", page, StringComparison.Ordinal);
        Assert.Contains($"<title>{Title} ", page, StringComparison.Ordinal);
        Assert.Contains($"{ScreenHeader.Name}</title>", page, StringComparison.Ordinal);
        Assert.Contains("<a class=\"skip-link\" href=\"#content\">", page, StringComparison.Ordinal);
        Assert.Contains($"href=\"{ScreenAssets.StyleRoute}\"", page, StringComparison.Ordinal);
        Assert.Contains("<header class=\"header\">", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APageIsKeptByNoCache()
    {
        using var response = await Client.GetAsync(new Uri(Page, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static RazorComponentResult<ScreenFrame> Show(HttpContext context) => ScreenResult.Of(context, typeof(ScreenHeader), Title);

    private static string Sniffing(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-Content-Type-Options", out var values) ? string.Join(',', values) : "";
}
