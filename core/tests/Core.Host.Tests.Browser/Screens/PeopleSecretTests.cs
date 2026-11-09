using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Core.Host.Tests.Browser.Screens.PeopleRegistrationTests;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// What the registration of a person (issue 599) does not leave behind, in a real Chromium over the host with the real route and the real registry: the initial password the administration
/// set is not in the page, in the address, in the answer, in the log of the host nor in the records of the database, neither after the registration nor after a refusal; and the notice that
/// the person was registered is told once, comes from no address, and is not a cookie a script can read.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeopleSecretTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private const string Cookie = "__Host-cadastrada";

    [Fact]
    public async Task TheInitialPasswordIsNotInThePageTheAddressTheAnswerTheLogNorTheRecordsAfterARegistration()
    {
        var (name, password) = (Made("segredo"), Made("senha-inicial").ToUpperInvariant());
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, name, "Anotação", password);

        var response = await SubmitAsync(tab.Page);

        Assert.Equal(303, response.Status);
        await Expect(tab.Page.Locator(".notice--success")).ToBeVisibleAsync();
        await AssertAbsentAsync(tab.Page, response, password);
    }

    [Fact]
    public async Task TheInitialPasswordIsNotInThePageTheAddressTheAnswerTheLogNorTheRecordsAfterARefusalNorGivenBackToTheField()
    {
        var password = Made("senha-inicial").ToUpperInvariant();
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, people.Host.Annotator, "Anotação", password);

        var response = await SubmitAsync(tab.Page);

        Assert.Equal(409, response.Status);
        await Expect(tab.Page.Locator("#pessoa-senha")).ToHaveValueAsync(string.Empty);
        await AssertAbsentAsync(tab.Page, response, password);
    }

    [Fact]
    public async Task TheNoticeThatThePersonWasRegisteredIsToldOnceAndAReloadOrANewVisitDoesNotTellItAgain()
    {
        var name = Made("aviso");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, name, "Anotação", people.Host.Password);
        await tab.Page.Locator("#cadastrar").ClickAsync();
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync($"{name} foi cadastrada");

        _ = await tab.Page.ReloadAsync();
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Pessoas", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".paged-list li")).ToHaveCountAsync(1);
        _ = await tab.Page.GotoAsync(List);

        await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        Assert.DoesNotContain(await tab.Page.Context.CookiesAsync(), cookie => cookie.Name == Cookie);
    }

    [Fact]
    public async Task NoAddressMakesThePageTellThatSomeoneWasRegisteredBecauseTheNoticeComesFromNoAddress()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, List);

        foreach (var forged in new[] { $"{List}?cadastrada=1&nome=zqx-forjada", $"{List}?cadastrada=zqx-forjada", $"{List}?nome=zqx-forjada&registrada=zqx-forjada", $"{List}?{Cookie}=zqx-forjada" })
        {
            _ = await tab.Page.GotoAsync(forged);

            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Pessoas", Level = 1 })).ToBeVisibleAsync();
            await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
            Assert.DoesNotContain("foi cadastrada", await tab.Page.ContentAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheNoticeIsNotACookieAScriptReadsAndTheAddressAfterTheRegistrationCarriesOnlyTheName()
    {
        var name = Made("cookie");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, name, "Anotação", people.Host.Password);

        var response = await SubmitAsync(tab.Page);

        Assert.Equal($"{List}?nome={name}", response.Headers["location"]);
        var setCookie = (await response.HeaderValuesAsync("set-cookie")).Single(header => header.StartsWith(Cookie, StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        await Expect(tab.Page.Locator(".notice--success")).ToBeVisibleAsync();
        Assert.DoesNotContain("cadastrada", await tab.Page.EvaluateAsync<string>("() => document.cookie"), StringComparison.Ordinal);
    }

    private async Task AssertAbsentAsync(IPage page, IResponse response, string password)
    {
        var headers = string.Join('\n', (await response.AllHeadersAsync()).Select(header => $"{header.Key}: {header.Value}"));
        var seen = string.Join('\n', page.Url, await page.ContentAsync(), headers, people.Host.Log, await people.Host.DumpAsync());

        Assert.DoesNotContain(password, seen, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(password), seen, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(Regex.Escape(password), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5)), seen);
    }
}
