using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The failure nobody handled in a screen (issue 671, CA-4): the server answers 500 with the base page, in its frame and with no detail of what failed, to whoever asked for a page;
/// whoever asked for something else, such as JSON, gets the 500 with no body.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FailurePageTests(BrowserFixture browser)
{
    private const int ServerFailure = 500;
    private const string Title = "Falha ao abrir a página";

    private static readonly string[] Detail = [TrialRoutes.InternalDetail, "InvalidOperationException", "Exception", "StackTrace", "   at ", ".cs:", "Core.Host", "Microsoft.AspNetCore"];

    [Fact]
    public async Task Ca4AFailureNobodyHandledIsTheBasePageWith500AndNoDetailInsideItsFrame()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);

        var response = await tab.Page.GotoAsync(TrialRoutes.Failure);
        var page = await tab.Page.ContentAsync();

        Assert.Equal(ServerFailure, response?.Status);
        await Expect(tab.Page).ToHaveTitleAsync(new Regex($"^{Title}"));
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = Title })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("html")).ToHaveAttributeAsync("lang", "pt-BR");
        await Expect(tab.Page.Locator("link[rel=stylesheet]")).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator("a.skip-link + header.header + main#content")).ToHaveCountAsync(1);
        Assert.All(Detail, term => Assert.DoesNotContain(term, page, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca4ThePageOfAFailureHasNoViolationOfWcagAAndAa(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        _ = await tab.Page.GotoAsync(TrialRoutes.Failure);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "falha-na-pagina-base");
    }

    [Fact]
    public async Task Ca4ARequestThatDoesNotAskForAPageGetsThe500WithNoBodyAndNoDetail()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        var address = new Uri(new Uri(tab.Page.Url), TrialRoutes.Failure).AbsoluteUri;

        var response = await tab.Page.Context.APIRequest.GetAsync(address, new() { Headers = new Dictionary<string, string> { ["Accept"] = "application/json" }, FailOnStatusCode = false });
        var body = await response.BodyAsync();

        Assert.Equal(ServerFailure, response.Status);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Ca4TheFailureOfAPostThatSwapsARegionIsShownAsTheBasePageWithNoDetail()
    {
        await using var tab = await browser.OpenAsync($"{TrialRoutes.Submit}/falha?key=zqx-falha-6710");

        await tab.Page.Locator("#enviar").ClickAsync();

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = Title })).ToBeVisibleAsync();
        var page = await tab.Page.ContentAsync();
        Assert.All(Detail, term => Assert.DoesNotContain(term, page, StringComparison.Ordinal));
    }
}
