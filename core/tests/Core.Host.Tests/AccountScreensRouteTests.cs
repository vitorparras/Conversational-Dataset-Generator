using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;

namespace Core.Host.Tests;

/// <summary>
/// Ending one session from the page of the account (issue 598, decision 0051), behind the real session gate and over the store of the test: the act accepts only a session the list offers,
/// which leaves out the current one, so the form answers as the address of the confirmation does.
/// </summary>
public sealed class AccountScreensRouteTests
{
    private const string Browser = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheActEndsAnotherSessionOfTheAskerAndTakesTheBrowserBackToTheAccount()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);
        var other = host.Sessions.Open(7);
        var listed = host.Sessions.ListOf(7, mine).Single(session => !session.Current).ListingId;

        using var request = Post(listed);
        using var answer = await host.SendAsync(request, mine, encrypted: true, Token);

        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.Account, 7L, null), (answer.StatusCode, answer.Headers.Location?.OriginalString, host.Sessions.Resolve(mine), host.Sessions.Resolve(other)));
    }

    [Fact]
    public async Task TheActRefusesTheCurrentSessionAsTheListDoesNotOfferItAndTheSessionStaysOpen()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);
        _ = host.Sessions.Open(7);
        var current = host.Sessions.ListOf(7, mine).Single(session => session.Current).ListingId;

        using var request = Post(current);
        using var answer = await host.SendAsync(request, mine, encrypted: true, Token);

        Assert.Equal((HttpStatusCode.NotFound, 7L), (answer.StatusCode, host.Sessions.Resolve(mine)));
    }

    [Fact]
    public async Task TheActRefusesASessionOfAnotherPersonAndAnIdentifierThatIsNoListingAndEndsNothing()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);
        var theirs = host.Sessions.Open(8);
        var theirListing = host.Sessions.ListOf(8, theirs).Single().ListingId;

        using var ofAnother = Post(theirListing);
        using var byIdentifier = Post(theirs);
        using var first = await host.SendAsync(ofAnother, mine, encrypted: true, Token);
        using var second = await host.SendAsync(byIdentifier, mine, encrypted: true, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, 8L), (first.StatusCode, second.StatusCode, host.Sessions.Resolve(theirs)));
    }

    private static HttpRequestMessage Post(string listed)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ScreenPaths.AccountEnd, UriKind.Relative))
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>(SessionEndScreen.SessionField, listed)]),
        };
        request.Headers.Accept.ParseAdd(Browser);
        return request;
    }

    private static Task<SessionedHost> StartAsync() => SessionedHost.StartAsync(services => services, AccountScreensEndpoint.Map, Token);
}
