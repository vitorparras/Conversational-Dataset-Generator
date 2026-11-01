using System.Net;
using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>
/// Every route but the entry and the status refuses a request with no session, with 401 and the route of the entry, whatever the cause (issues #70 and #495).
/// The host here has no database: the entry then says it cannot, and no request gets in.
/// </summary>
public sealed class SessionGateTests
{
    private const string Cookie = "__Host-sessao";

    public static TheoryData<string> BodiesThatAreNotANameAndAPassword =>
    [
        JsonSerializer.Serialize(new { name = "zqx-pessoa" }),
        JsonSerializer.Serialize(new { name = (string?)null, password = "zqx-senha" }),
        JsonSerializer.Serialize(new { name = "zqx-pessoa", password = "zqx-senha", extra = 1 }),
    ];

    [Fact]
    public async Task RequestWithoutACookieToARouteThatRequiresASessionGets401PointingAtTheEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/session", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("/session", await SignInOfAsync(response, cancellationToken));
    }

    [Fact]
    public async Task RequestWithAnIdentifierNoSessionHasGetsTheSame401()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/session", UriKind.Relative));
        request.Headers.Add("Cookie", $"{Cookie}={new string('0', 64)}");

        using var response = await host.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("/session", await SignInOfAsync(response, cancellationToken));
    }

    [Fact]
    public async Task RoutesThatRequireNoSessionAreServedWithNone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EntryOnAHostWithNoDatabaseAnswers503AndOpensNoSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        var password = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);

        using var response = await host.PostAsync("/session", JsonSerializer.Serialize(new { name = "zqx-pessoa", password }), cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [MemberData(nameof(BodiesThatAreNotANameAndAPassword))]
    public async Task EntryWithABodyThatIsNotANameAndAPasswordAnswers400(string body)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.PostAsync("/session", body, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExitWithNoSessionIsRefusedLikeEveryRouteBehindTheGate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri("/session", UriKind.Relative));

        using var response = await host.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("/session", await SignInOfAsync(response, cancellationToken));
    }

    public static TheoryData<string, string> RoutesBehindTheGate => MappedRoutes.Gated();

    [Theory]
    [MemberData(nameof(RoutesBehindTheGate))]
    public async Task EveryRouteBehindTheGateAnswers401PointingAtTheEntryWhenThereIsNoSession(string method, string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(route, UriKind.Relative));

        using var response = await host.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("/session", await SignInOfAsync(response, cancellationToken));
    }

    private static async Task<string?> SignInOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.GetProperty("signIn").GetString();
    }
}
