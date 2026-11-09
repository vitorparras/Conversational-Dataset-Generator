using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// Who reaches the screens of the people (issue 599, CA-4) in a real Chromium, over the host with the real session gate and the real screen gate: the technical administration alone. A person
/// of another function, on the list, on the form and on the post of the form, gets the page of an address that does not exist, the same bytes but for the address, and the post registers nobody.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeopleAccessTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private static readonly string[] Routes = [List, Form, $"{List}?nome=zqx-lista-&pagina=2"];

    [Theory]
    [InlineData("Auditor")]
    [InlineData("Curator")]
    [InlineData("Annotator")]
    public async Task Ca4ForAPersonWhoIsNotOfTheAdministrationEveryAddressOfThePeopleAnswersNotFoundWithTheSamePageAsOneThatDoesNotExist(string who)
    {
        await using var tab = await OpenAsync(browser, people.Host, Person(who), "/");
        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();

        foreach (var route in Routes)
        {
            var answer = await tab.Page.GotoAsync(route);

            Assert.Equal(StatusNotFound, answer?.Status);
            Assert.Equal(StatusNotFound, missing?.Status);
            Assert.Equal(Stripped(missingPage, Missing), Stripped(await tab.Page.ContentAsync(), route));
        }
    }

    [Theory]
    [InlineData("Auditor")]
    [InlineData("Curator")]
    [InlineData("Annotator")]
    public async Task Ca4ThePostOfTheFormAnswersNotFoundForAPersonWhoIsNotOfTheAdministrationAndRegistersNobody(string who)
    {
        var name = Made("recusada");
        await using var tab = await OpenAsync(browser, people.Host, Person(who), "/");
        await using var administration = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{List}?nome={name}");

        var posted = await tab.Page.Context.APIRequest.PostAsync(
            new Uri(new Uri(tab.Page.Url), Form).AbsoluteUri,
            new() { Headers = new Dictionary<string, string> { ["Accept"] = "text/html" }, Form = Asked(tab.Page, name) });

        Assert.Equal(StatusNotFound, posted.Status);
        _ = await administration.Page.ReloadAsync();
        await Expect(administration.Page.Locator(".empty-state")).ToContainTextAsync("Nenhuma pessoa com esse filtro.");
    }

    [Theory]
    [InlineData("Auditor")]
    [InlineData("Curator")]
    [InlineData("Annotator")]
    public async Task Ca4TheMenuOfAPersonWhoIsNotOfTheAdministrationHasNoEntryForThePeople(string who)
    {
        await using var tab = await OpenAsync(browser, people.Host, Person(who), "/");

        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Pessoas" })).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Cadastrar pessoa" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheAdministrationReachesTheListAndTheFormWithStatusOk()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, "/");

        var list = await tab.Page.GotoAsync(List);
        var form = await tab.Page.GotoAsync(Form);

        Assert.Equal([200, 200], [list?.Status, form?.Status]);
    }

    private static IFormData Asked(IPage page, string name) => page.Context.APIRequest.CreateFormData().Set("nome", name).Set("função", "Anotação").Set("senha", "ZQX-x");

    private static string Stripped(string page, string route) => page.Replace(route.TrimStart('/'), string.Empty, StringComparison.Ordinal);

    private string Person(string who) => who switch
    {
        "Auditor" => people.Host.Auditor,
        "Curator" => people.Host.Curator,
        _ => people.Host.Annotator,
    };
}
