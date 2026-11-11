using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Microsoft.AspNetCore.Builder;

namespace Core.Host.Tests;

/// <summary>
/// The page of the address of another person, and its correction, have the access of the administration (issue 635, part 3, policy 009): whoever enters with no function that reaches them gets
/// the answer of a route that does not exist, the same status and the same body, as for the reading as for the sending, and the sending reaches no registry.
/// </summary>
public sealed class OperatorEmailRouteTests
{
    private const string Missing = "/zqx-nao-existe-6350";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task WhoEnteredWithNoFunctionThatReachesThePageGetsTheAnswerOfARouteThatDoesNotExist(string method)
    {
        await using var host = await SessionedHost.StartAsync(
            services => services,
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                OperatorEmailEndpoint.Map(routes);
            },
            Token);
        var identifier = host.SeedSession();

        using var refused = await host.SendAsync(Request(method, OperatorEmailEndpoint.Of(7)), identifier, encrypted: true, Token);
        using var missing = await host.SendAsync(Request(method, Missing), identifier, encrypted: true, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (refused.StatusCode, missing.StatusCode));
        Assert.Equal(await missing.Content.ReadAsByteArrayAsync(Token), await refused.Content.ReadAsByteArrayAsync(Token));
    }

    private static HttpRequestMessage Request(string method, string route)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), new Uri(route, UriKind.Relative));
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
        if (method == "POST")
        {
            request.Content = new FormUrlEncodedContent([KeyValuePair.Create(AccountEmailScreen.EmailField, "alheio@exemplo.org")]);
        }

        return request;
    }
}
