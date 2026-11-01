using System.Net;
using System.Net.Sockets;
using System.Text;
using Core.Host.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// What the session leaves of the evidence the issue on the encrypted channel asks for (#72, CA-4): the cookie carries the attributes that keep it on the channel
/// and out of scripts, the port speaks only TLS, and an attempt in clear that presents the cookie dies at the negotiation without an answer.
/// </summary>
public sealed class SessionChannelEvidenceTests
{
    private const string Identifier = "ZQX0123456789ABCDEF";

    [Fact]
    public void CookieIssuedIsSecureHttpOnlyStrictForTheWholeHostAndHasNoDomain()
    {
        var context = new DefaultHttpContext();

        SessionCookie.Issue(context.Response, Identifier);

        var cookie = Assert.Single(context.Response.Headers.SetCookie.ToArray());
        Assert.StartsWith($"__Host-sessao={Identifier};", cookie, StringComparison.Ordinal);
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CookieClearedKeepsTheAttributesThatMakeTheBrowserTakeIt()
    {
        var context = new DefaultHttpContext();

        SessionCookie.Clear(context.Response);

        var cookie = Assert.Single(context.Response.Headers.SetCookie.ToArray());
        Assert.StartsWith("__Host-sessao=;", cookie, StringComparison.Ordinal);
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AttemptInClearThatPresentsTheCookieDiesAtTheNegotiationWithNoAnswer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, host.BaseAddress.Port, cancellationToken);
        var stream = tcp.GetStream();
        var request = $"GET /session HTTP/1.1\r\nHost: x\r\nCookie: __Host-sessao={Identifier}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellationToken);

        var answered = await ReadToEndAsync(stream, cancellationToken);

        Assert.DoesNotContain("HTTP/", answered, StringComparison.Ordinal);
        Assert.DoesNotContain("signIn", answered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SamePortServesTheRouteOverTheEncryptedChannel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/session", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        var read = new MemoryStream();
        try
        {
            await stream.CopyToAsync(read, limit.Token);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            // A connection the host reset, or one that stayed silent, is also an answer without content.
        }

        return Encoding.ASCII.GetString(read.ToArray());
    }
}
