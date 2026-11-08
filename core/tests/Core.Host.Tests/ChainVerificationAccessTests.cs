using System.Net;
using Core.Host.Endpoints;
using Microsoft.AspNetCore.Builder;

namespace Core.Host.Tests;

/// <summary>
/// The reading of the verification of the chain by JSON has the access of its screen (#605, policy 006): the history names who asked for each verification, so whoever enters with no
/// function that reaches the screen gets the answer of a route that does not exist, 404 with no body, as for the page.
/// </summary>
public sealed class ChainVerificationAccessTests
{
    [Fact]
    public async Task WhoEnteredWithNoFunctionThatReachesTheScreenGetsA404WithNoBodyAsOfARouteThatDoesNotExist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await SessionedHost.StartAsync(
            services => services,
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                ChainVerificationEndpoint.Map(routes);
            },
            cancellationToken);
        var identifier = host.SeedSession();

        using var refused = await host.GetAsync(ChainVerificationEndpoint.Route, identifier, encrypted: true, cancellationToken);
        using var missing = await host.GetAsync("/zqx-nao-existe-6050", identifier, encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (refused.StatusCode, missing.StatusCode));
        Assert.Equal(await missing.Content.ReadAsByteArrayAsync(cancellationToken), await refused.Content.ReadAsByteArrayAsync(cancellationToken));
    }
}
