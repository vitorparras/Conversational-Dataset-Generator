using Core.Host.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// The notice of what the last act on the own account did (#597, #598): it comes only from the cookie the act sets, never from the address, holds a count of zero or more, and is
/// told once.
/// </summary>
public sealed class AccountNoticeTests
{
    [Fact]
    public void AnAddressThatSaysAnActWasDoneMakesNoNotice()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?feito=senha&encerradas=3");

        Assert.Null(AccountNotice.Take(context));
    }

    [Fact]
    public void TheNoticeTheActSetsIsTheActAndTheCountAndIsSecureHttpOnlyAndStrict()
    {
        var act = new DefaultHttpContext();

        AccountNotice.Issue(act.Response, "sessoes", 2);

        var line = act.Response.Headers.SetCookie.ToString();
        Assert.Equal(("sessoes", 2), AccountNotice.Take(Asked(line)));
        Assert.Contains("secure", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", line, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("senha:-3")]
    [InlineData("senha:+3")]
    [InlineData("senha:muitas")]
    [InlineData("senha")]
    [InlineData("senha:1:2")]
    public void ANoticeWhoseCountIsNotAWholeNumberOfZeroOrMoreSaysNothing(string notice) =>
        Assert.Null(AccountNotice.Take(Asked($"{AccountNotice.Name}={Uri.EscapeDataString(notice)};")));

    [Fact]
    public void TheAccountTellsTheNoticeOnceAndThenForgetsIt()
    {
        var act = new DefaultHttpContext();
        AccountNotice.Issue(act.Response, "senha", 0);
        var account = Asked(act.Response.Headers.SetCookie.ToString());

        _ = AccountNotice.Take(account);

        var forgotten = account.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{AccountNotice.Name}=;", forgotten, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", forgotten, StringComparison.Ordinal);
    }

    private static DefaultHttpContext Asked(string setCookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = setCookie[..setCookie.IndexOf(';', StringComparison.Ordinal)];
        return context;
    }
}
