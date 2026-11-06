using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// A failure no route handled (#671, CA-4), in process behind the real gates: a request for a page gets 500 on the base page, with the frame and a way back, and nothing
/// of the failure, neither its message nor its kind, and with the header of the encrypted channel; the log names the kind and not the message.
/// Any other request keeps the 500 of the server, with no body.
/// </summary>
public sealed class ScreenFailureTests
{
    private const string Failing = "/zqx-falha-6710";
    private const string Secret = "zqx-detalhe-interno-6710";
    private const string BrowserAccept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    [Fact]
    public async Task APageThatFailsIsA500OnTheBasePageWithNothingOfTheFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var log = new RecordedLog();
        await using var host = await StartAsync(log, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Failing, UriKind.Relative));
        request.Headers.Accept.ParseAdd(BrowserAccept);

        using var response = await host.SendAsync(request, host.SeedSession(), encrypted: true, cancellationToken);
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["max-age=31536000"], response.Headers.GetValues("Strict-Transport-Security"));
        Assert.Contains(FailureScreen.Title, page, StringComparison.Ordinal);
        Assert.Contains($"href=\"{ScreenPaths.Home}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, page, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), page, StringComparison.Ordinal);
        Assert.Contains(log.Lines, line => line.Contains(typeof(InvalidOperationException).FullName!, StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, line => line.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARequestThatDoesNotAskForAPageKeepsThe500OfTheServerWithNoBody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var log = new RecordedLog();
        await using var host = await StartAsync(log, cancellationToken);

        using var response = await host.GetAsync(Failing, host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.InternalServerError, 0L), (response.StatusCode, (await response.Content.ReadAsByteArrayAsync(cancellationToken)).LongLength));
    }

    private static Task<SessionedHost> StartAsync(ILoggerProvider log, CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services.AddSingleton(log),
            routes =>
            {
                TransportSecurity.Map((IApplicationBuilder)routes);
                ScreenFailure.Use((IApplicationBuilder)routes);
                ScreenGate.Use((IApplicationBuilder)routes);
                _ = routes.MapGet(Failing, Fail).WithMetadata(ScreenAccess.Everyone);
            },
            cancellationToken);

    private static IResult Fail() => throw new InvalidOperationException(Secret);
}
