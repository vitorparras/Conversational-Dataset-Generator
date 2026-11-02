using System.Net;
using System.Text.Json;
using Core.Host.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// An identifier of session presented without encryption is refused and ends the session (issue #72, CA-4), proved by the gate the host runs: a client that is no
/// browser presents the cookie at a port in clear, and the same session is then refused on the encrypted channel too, because it can have been seen.
/// </summary>
public sealed class SessionInClearTests
{
    private const string Route = "/zqx-protected-7731";

    [Fact]
    public async Task SessionPresentedOverTheEncryptedChannelIsLetThrough()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.GetAsync(Route, host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task IdentifierPresentedInClearIsRefusedPointingAtTheEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.GetAsync(Route, host.SeedSession(), encrypted: false, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(SessionEndpoint.Route, body.RootElement.GetProperty("signIn").GetString());
    }

    [Fact]
    public async Task SessionWhoseIdentifierWasPresentedInClearIsRefusedOnTheEncryptedChannelToo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var inClear = await host.GetAsync(Route, identifier, encrypted: false, cancellationToken);
        using var afterwards = await host.GetAsync(Route, identifier, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, inClear.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task IdentifierPresentedInClearToTheRouteThatNeedsNoSessionIsRefusedAndEndsTheSessionAsWell()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var identifier = host.SeedSession();

        using var inClear = await host.GetAsync(StatusEndpoint.Route, identifier, encrypted: false, cancellationToken);
        using var afterwards = await host.GetAsync(Route, identifier, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, inClear.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task RequestInClearWithNoIdentifierIsNotRefusedByTheEncryptionRule()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);

        using var response = await host.GetAsync(StatusEndpoint.Route, identifier: null, encrypted: false, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task OtherSessionStaysOpenWhenOneIsEndedByBeingPresentedInClear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(cancellationToken);
        var seen = host.SeedSession();
        var other = host.SeedSession();

        using var inClear = await host.GetAsync(Route, seen, encrypted: false, cancellationToken);
        using var untouched = await host.GetAsync(Route, other, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, untouched.StatusCode);
    }

    /// <summary>The gate in front of one route that needs a session and, marked open as the real status is, one that does not.</summary>
    private static Task<SessionedHost> StartAsync(CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services,
            routes =>
            {
                _ = routes.MapGet(Route, () => Results.NoContent());
                _ = routes.MapGet(StatusEndpoint.Route, () => Results.NoContent()).WithMetadata(new SessionOpen());
            },
            cancellationToken);
}
