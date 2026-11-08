using Core.Host.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Core.Host.Tests;

/// <summary>
/// The list of people as the address asks for it (#599): a function the registry does not accept is left out of the filter, and the notice that someone was registered comes only from the
/// cookie the registration sets, which the list tells once; no address makes the page say it.
/// </summary>
public sealed class OperatorScreensTests
{
    [Fact]
    public void AFunctionTheRegistryDoesNotAcceptIsLeftOutOfTheFilter()
    {
        var filter = OperatorScreensEndpoint.FilterOf(Query(("nome", "ana"), ("funcao", "Inexistente"), ("estado", "ativas")));

        Assert.Equal(("ana", null, true), (filter.Name, filter.Function, filter.Active));
    }

    [Fact]
    public void AFunctionTheRegistryAcceptsNarrowsTheFilter()
    {
        var filter = OperatorScreensEndpoint.FilterOf(Query(("funcao", "Auditoria")));

        Assert.Equal("Auditoria", filter.Function);
    }

    [Fact]
    public void AnAddressThatSaysSomeoneWasRegisteredMakesNoNotice()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?nome=Maria&cadastrada=1");

        Assert.Null(RegisteredNotice.Take(context));
    }

    [Fact]
    public void TheNoticeTheRegistrationSetsHoldsOnlyTheIdentifierAndIsSecureHttpOnlyAndStrict()
    {
        var registration = new DefaultHttpContext();

        RegisteredNotice.Issue(registration.Response, 9_007_199_254_740_993);

        var line = registration.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{RegisteredNotice.Name}=9007199254740993;", line, StringComparison.Ordinal);
        Assert.Equal(9_007_199_254_740_993, RegisteredNotice.Take(ListAsked(line)));
        Assert.Contains("secure", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", line, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Maria")]
    [InlineData("0")]
    [InlineData("-4")]
    [InlineData("+4")]
    [InlineData("4 5")]
    public void ANoticeThatHoldsNoIdentifierSaysNothing(string held) =>
        Assert.Null(RegisteredNotice.Take(ListAsked($"{RegisteredNotice.Name}={Uri.EscapeDataString(held)};")));

    [Fact]
    public void TheListTellsTheNoticeOnceAndThenForgetsIt()
    {
        var registration = new DefaultHttpContext();
        RegisteredNotice.Issue(registration.Response, 42);
        var list = ListAsked(registration.Response.Headers.SetCookie.ToString());

        _ = RegisteredNotice.Take(list);

        var forgotten = list.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{RegisteredNotice.Name}=;", forgotten, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", forgotten, StringComparison.Ordinal);
    }

    private static DefaultHttpContext ListAsked(string setCookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = setCookie[..setCookie.IndexOf(';', StringComparison.Ordinal)];
        return context;
    }

    private static QueryCollection Query(params (string Key, string Value)[] pairs) =>
        new(pairs.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value), StringComparer.Ordinal));
}
