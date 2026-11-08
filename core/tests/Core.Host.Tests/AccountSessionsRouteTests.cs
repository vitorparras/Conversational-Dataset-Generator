using System.Net;
using System.Text.Json;
using Core.Host.Endpoints;

namespace Core.Host.Tests;

/// <summary>
/// The routes of the sessions of the person of the session (issue 598), behind the real gate and over the store of the test:
/// only their own, by a listing identifier that is not the one of the session.
/// </summary>
public sealed class AccountSessionsRouteTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheListHoldsTheSessionsOfTheAskerOnlyWithTheCurrentOneMarkedAndNoSessionIdentifier()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);
        var also = host.Sessions.Open(7);
        var theirs = host.Sessions.Open(8);

        using var listed = await host.GetAsync("/account/sessions", mine, encrypted: true, Token);

        var body = await listed.Content.ReadAsStringAsync(Token);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items.Count(item => item.GetProperty("current").GetBoolean()));
        Assert.All(items, item => Assert.Equal(["current", "id", "lastActivity", "start"], item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.All([mine, also, theirs], identifier => Assert.DoesNotContain(identifier, body, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EndingASessionByItsListingIdentifierMakesTheStoreRefuseItAndLeavesTheAskerAndTheOthers()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);
        var second = host.Sessions.Open(7);
        var theirs = host.Sessions.Open(8);
        var id = (await ListingsAsync(host, mine)).Single(item => !item.Current).Id;

        using var ended = await host.DeleteAsync($"/account/sessions/{id}", mine, Token);

        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        Assert.Equal(7, host.Sessions.Resolve(mine));
        Assert.Null(host.Sessions.Resolve(second));
        Assert.Equal(8, host.Sessions.Resolve(theirs));
    }

    [Fact]
    public async Task AnotherPersonsListingIdentifierAndASessionIdentifierEndNothingAndAreNotFound()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);
        var theirs = host.Sessions.Open(8);
        var theirListing = (await ListingsAsync(host, theirs)).Single().Id;

        using var byListing = await host.DeleteAsync($"/account/sessions/{theirListing}", mine, Token);
        using var bySession = await host.DeleteAsync($"/account/sessions/{theirs}", mine, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (byListing.StatusCode, bySession.StatusCode));
        Assert.Equal(8, host.Sessions.Resolve(theirs));
    }

    [Fact]
    public async Task WithNoSessionTheRoutesAnswerUnauthorized()
    {
        await using var host = await StartAsync();

        using var listed = await host.GetAsync("/account/sessions", identifier: null, encrypted: true, Token);
        using var ended = await host.DeleteAsync("/account/sessions/abc", identifier: null, Token);
        using var changed = await host.PostAsync("/account/password", ChangeBody, identifier: null, Token);

        Assert.Equal(3, new[] { listed, ended, changed }.Count(response => response.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task WithNoDatabaseDeclaredTheChangeOfThePasswordSaysThatTheHostCannot()
    {
        await using var host = await StartAsync();
        var mine = host.Sessions.Open(7);

        using var changed = await host.PostAsync("/account/password", ChangeBody, mine, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, changed.StatusCode);
    }

    private static string ChangeBody => JsonSerializer.Serialize(new { current = "a", replacement = "b" });

    private static Task<SessionedHost> StartAsync() => SessionedHost.StartAsync(services => services, AccountEndpoint.Map, Token);

    private static async Task<List<Listed>> ListingsAsync(SessionedHost host, string asker)
    {
        using var listed = await host.GetAsync("/account/sessions", asker, encrypted: true, Token);
        var items = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(Token)).RootElement.EnumerateArray();
        return [.. items.Select(item => new Listed(item.GetProperty("id").GetString()!, item.GetProperty("current").GetBoolean()))];
    }

    private sealed record Listed(string Id, bool Current);
}
