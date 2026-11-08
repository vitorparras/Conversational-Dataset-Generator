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
    public void TheNoticeTheRegistrationSetsIsTheNameItWasGivenAndIsSecureHttpOnlyAndStrict()
    {
        var registration = new DefaultHttpContext();

        RegisteredNotice.Issue(registration.Response, "Maria José da Silva");

        var line = registration.Response.Headers.SetCookie.ToString();
        Assert.Equal("Maria José da Silva", RegisteredNotice.Take(ListAsked(line)));
        Assert.Contains("secure", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheListTellsTheNoticeOnceAndThenForgetsIt()
    {
        var registration = new DefaultHttpContext();
        RegisteredNotice.Issue(registration.Response, "Maria");
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
