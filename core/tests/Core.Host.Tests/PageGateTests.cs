using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Host.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// What the gate does with a request that has no session when the request is for a page (#592): a browser asking for HTML with a GET or a form goes to the entry with 303,
/// whether the route exists or not, so that going there reveals nothing about which pages exist, and every other request keeps its answer, 401 in JSON for a route
/// behind the gate and 404 for a route that does not exist.
/// </summary>
public sealed class PageGateTests
{
    private const string Protected = "/zqx-protegida-7731";
    private const string Missing = "/zqx-nao-existe-7731";
    private const string Open = "/zqx-aberta-7731";
    private const string BrowserAccept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    [Theory]
    [InlineData(Protected)]
    [InlineData(Missing)]
    public async Task APageRequestWithNoSessionGoesToTheEntryWith303WhetherTheRouteExistsOrNot(string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.SendAsync(Page(HttpMethod.Get, route), identifier: null, encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.SignIn), (response.StatusCode, response.Headers.Location?.OriginalString));
    }

    [Theory]
    [InlineData(Protected)]
    [InlineData(Missing)]
    public async Task AFormPostedWithNoSessionGoesToTheEntryWith303WhetherTheRouteExistsOrNot(string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var form = Page(HttpMethod.Post, route);
        form.Content = new FormUrlEncodedContent([KeyValuePair.Create("zqx", "1")]);

        using var response = await host.SendAsync(form, identifier: null, encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.SignIn), (response.StatusCode, response.Headers.Location?.OriginalString));
    }

    [Fact]
    public async Task AnIdentifierNoSessionHasIsTakenToTheEntryAsAPageRequestWithNoSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.SendAsync(Page(HttpMethod.Get, Protected), new string('0', 64), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.SignIn), (response.StatusCode, response.Headers.Location?.OriginalString));
    }

    [Fact]
    public async Task APageRequestWhoseIdentifierCameInClearGoesToTheEntryAndTheSessionIsEnded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var inClear = await host.SendAsync(Page(HttpMethod.Get, Protected), identifier, encrypted: false, cancellationToken);
        using var afterwards = await host.SendAsync(Page(HttpMethod.Get, Protected), identifier, encrypted: true, cancellationToken);

        Assert.Equal(
            [(HttpStatusCode.SeeOther, ScreenPaths.SignIn), (HttpStatusCode.SeeOther, ScreenPaths.SignIn)],
            [(inClear.StatusCode, inClear.Headers.Location?.OriginalString), (afterwards.StatusCode, afterwards.Headers.Location?.OriginalString)]);
    }

    [Fact]
    public async Task ARequestThatDoesNotAskForHtmlKeepsTheJsonRefusalOfARouteBehindTheGate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        using var anything = new HttpRequestMessage(HttpMethod.Get, new Uri(Protected, UriKind.Relative));
        anything.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        using var response = await host.SendAsync(anything, identifier: null, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(SessionEndpoint.Route, body.RootElement.GetProperty("signIn").GetString());
    }

    [Fact]
    public async Task ARequestThatDoesNotAskForHtmlToARouteThatDoesNotExistStaysA404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.GetAsync(Missing, identifier: null, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task APageRequestWithASessionIsServedAndOneToARouteThatDoesNotExistIsA404NotAnEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var served = await host.SendAsync(Page(HttpMethod.Get, Protected), identifier, encrypted: true, cancellationToken);
        using var missing = await host.SendAsync(Page(HttpMethod.Get, Missing), identifier, encrypted: true, cancellationToken);

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.NotFound], [served.StatusCode, missing.StatusCode]);
    }

    [Fact]
    public async Task APageRequestToARouteMarkedOpenIsServedWithNoSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.SendAsync(Page(HttpMethod.Get, Open), identifier: null, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static HttpRequestMessage Page(HttpMethod method, string route)
    {
        var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        request.Headers.Accept.ParseAdd(BrowserAccept);
        return request;
    }

    private static Task<SessionedHost> StartAsync(CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services,
            routes =>
            {
                _ = routes.MapGet(Protected, () => Results.NoContent());
                _ = routes.MapGet(Open, () => Results.NoContent()).WithMetadata(new SessionOpen("the test of the gate for pages"));
            },
            cancellationToken);
}
