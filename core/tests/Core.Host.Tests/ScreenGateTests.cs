using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Tests;

/// <summary>
/// The access policy of the screens in process (#594), behind the real session gate and the screen gate, with no database: with no registry there is no function,
/// so a screen of a function is refused. A screen refused, a page of a route that declares no access and a page of a route that does not exist get the same answer,
/// status and body to the byte (CA-2 of #594, CA-5 of #590); the start page of whoever entered with no function and no domain lists nothing and has no menu (CA-1).
/// </summary>
public sealed class ScreenGateTests
{
    private const string OfAudit = "/zqx-auditoria-5940";
    private const string Unmarked = "/zqx-sem-marca-5940";
    private const string Missing = "/zqx-nao-existe-5940";
    private const string BrowserAccept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    [Theory]
    [InlineData(OfAudit)]
    [InlineData(Unmarked)]
    public async Task APageRefusedIsAnsweredAsARouteThatDoesNotExistToTheByte(string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var refused = await host.SendAsync(Page(route), identifier, encrypted: true, cancellationToken);
        using var missing = await host.SendAsync(Page(Missing), identifier, encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (refused.StatusCode, missing.StatusCode));
        Assert.Equal(await missing.Content.ReadAsByteArrayAsync(cancellationToken), await refused.Content.ReadAsByteArrayAsync(cancellationToken));
        Assert.Contains(NotFoundScreen.Title, WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync(cancellationToken)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequestThatDoesNotAskForHtmlToAScreenRefusedIsA404WithNoBodyAsARouteThatDoesNotExist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var refused = await host.GetAsync(OfAudit, identifier, encrypted: true, cancellationToken);
        using var missing = await host.GetAsync(Missing, identifier, encrypted: true, cancellationToken);

        Assert.Equal(
            [(HttpStatusCode.NotFound, 0L), (HttpStatusCode.NotFound, 0L)],
            [(refused.StatusCode, (await refused.Content.ReadAsByteArrayAsync(cancellationToken)).LongLength),
             (missing.StatusCode, (await missing.Content.ReadAsByteArrayAsync(cancellationToken)).LongLength)]);
    }

    [Fact]
    public async Task TheStartOfWhoeverHasNoFunctionAndNoDomainListsNothingWithNoMenuAndNoSelectorAndNoError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var start = await host.SendAsync(Page(ScreenPaths.Home), identifier, encrypted: true, cancellationToken);
        var page = await start.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Contains("class=\"empty-state\"", page, StringComparison.Ordinal);
        Assert.Contains("Nenhum domínio foi concedido a você", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<nav", page, StringComparison.Ordinal);
        Assert.DoesNotContain(DomainPicker.Field, page, StringComparison.Ordinal);
    }

    private static HttpRequestMessage Page(string route)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        request.Headers.Accept.ParseAdd(BrowserAccept);
        return request;
    }

    private static Task<SessionedHost> StartAsync(CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services,
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                _ = routes.MapGet(ScreenPaths.Home, Start).WithMetadata(ScreenAccess.Everyone);
                _ = routes.MapGet(OfAudit, Start).WithMetadata(ScreenAccess.Of("Auditoria", 1, PersonFunctions.Audit));
                _ = routes.MapGet(Unmarked, Start);
            },
            cancellationToken);

    private static RazorComponentResult<ScreenFrame> Start(HttpContext context) => ScreenResult.Of(context, typeof(HomeScreen), "Início");
}
