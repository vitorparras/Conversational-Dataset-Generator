using System.Net;
using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>
/// The routes that require a session refuse a request with none, with 401 and the route of the entry, whatever the cause (issue #70, CA-5 interim).
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

        using var response = await host.PostAsync("/session", JsonSerializer.Serialize(new { name = "zqx-pessoa", password = "zqx-senha" }), cancellationToken);

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
    public async Task ExitWithNoSessionAnswers204AndAsksTheBrowserToDropTheCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri("/session", UriKind.Relative));

        using var response = await host.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith($"{Cookie}=;", StringComparison.Ordinal));
    }

    private static async Task<string?> SignInOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.GetProperty("signIn").GetString();
    }
}
