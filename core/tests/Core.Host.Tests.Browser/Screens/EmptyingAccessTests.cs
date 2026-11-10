using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.EmptyingPages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// Who reaches the screens of the emptying (issue 617, CA-5) in a real Chromium, over the host with the real session gate and the real screen gate: the curation of the collection with a domain
/// granted alone. A person of another function, or of the curation with no domain granted, on the entry, on the confirmation and on the act, gets the page of an address that does not
/// exist, the same but for the address, and the act empties nothing.
/// </summary>
[Trait("Category", "Browser")]
public sealed class EmptyingAccessTests(BrowserFixture browser, EmptyingHostFixture emptying) : IClassFixture<EmptyingHostFixture>
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    [InlineData("Annotator")]
    [InlineData("UngrantedCurator")]
    public async Task Ca5ForAPersonWhoIsNotOfTheCurationWithADomainEveryAddressOfTheEmptyingAnswersNotFoundWithTheSamePageAsOneThatDoesNotExist(string who)
    {
        await using var tab = await OpenAsync(browser, emptying.Host, Person(who), "/");
        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();

        foreach (var route in new[] { Entry, EntryOf(1), ConfirmOf(1), $"{Confirm}?item=Maria" })
        {
            var answer = await tab.Page.GotoAsync(route);

            Assert.Equal((StatusNotFound, StatusNotFound), (answer?.Status, missing?.Status));
            Assert.Equal(missingPage.Replace(Missing.TrimStart('/'), string.Empty, StringComparison.Ordinal), await tab.Page.ContentAsync());
        }
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    [InlineData("Annotator")]
    [InlineData("UngrantedCurator")]
    public async Task Ca5TheActAnswersNotFoundForAPersonWhoIsNotOfTheCurationWithADomainAndEmptiesNothing(string who)
    {
        var item = emptying.Host.NextItem();
        await using var tab = await OpenAsync(browser, emptying.Host, Person(who), "/");

        var posted = await tab.Page.Context.APIRequest.PostAsync(
            new Uri(new Uri(tab.Page.Url), Confirm).AbsoluteUri,
            new() { Headers = new Dictionary<string, string> { ["Accept"] = "text/html" }, Form = Asked(tab.Page, item), MaxRedirects = 0 });

        Assert.Equal(StatusNotFound, posted.Status);
        Assert.Empty(await emptying.Host.EventsOfAsync(item));
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    [InlineData("Annotator")]
    [InlineData("UngrantedCurator")]
    public async Task Ca5TheMenuOfAPersonWhoIsNotOfTheCurationWithADomainHasNoEntryForTheEmptying(string who)
    {
        await using var tab = await OpenAsync(browser, emptying.Host, Person(who), "/");

        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Esvaziar item" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheCurationWithADomainReachesTheEntryAndTheConfirmationWithStatusOk()
    {
        var item = emptying.Host.NextItem();
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, "/");

        var entry = await tab.Page.GotoAsync(Entry);
        var confirmation = await tab.Page.GotoAsync(ConfirmOf(item));

        Assert.Equal([200, 200], [entry?.Status, confirmation?.Status]);
        Assert.Empty(await emptying.Host.EventsOfAsync(item));
    }

    private static IFormData Asked(IPage page, int item) =>
        page.Context.APIRequest.CreateFormData().Set("item", $"{item}").Set("motivo", ByTheSubject).Set("protocolo", Protocol());

    private string Person(string who) => who switch
    {
        "Administrator" => emptying.Host.Administrator,
        "Auditor" => emptying.Host.Auditor,
        "Annotator" => emptying.Host.Annotator,
        _ => emptying.Host.UngrantedCurator,
    };
}
