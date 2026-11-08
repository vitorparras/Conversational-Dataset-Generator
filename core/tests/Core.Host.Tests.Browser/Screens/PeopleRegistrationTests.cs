using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The form that registers a person (issue 599, CA-1 and CA-3) in a real Chromium, over the host with the real gates, the real route and the real registry: the person is made with the function
/// declared and no grant of any domain, enters with the password the administration set and finds the start empty; a name already used, a name or a password that is blank and a function
/// not chosen send the form back with the error bound to its field, with the status that says it, and with what was typed but never the password.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeopleRegistrationTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private const string Annotation = "Anotação";

    [Theory]
    [InlineData("Anotação")]
    [InlineData("Arbitragem")]
    [InlineData("Curadoria do acervo")]
    [InlineData("Avaliação")]
    [InlineData("Auditoria")]
    [InlineData("Administração técnica")]
    public async Task Ca1ThePersonRegisteredExistsWithTheFunctionDeclaredActiveAndTheNoticeSaysItWasRegisteredWithNoGrant(string function)
    {
        var name = Made("cadastrada");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);

        await FillAsync(tab.Page, name, function, people.Host.Password);
        await tab.Page.Locator("#cadastrar").ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"/pessoas\\?nome={name}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync($"{name} foi cadastrada, sem nenhuma concessão de domínio.");
        Assert.Equal([[name, function, "Ativa"]], await RowsAsync(tab.Page));
    }

    [Fact]
    public async Task Ca1ThePersonRegisteredEntersWithTheInitialPasswordAndSeesTheStartEmptyWithNoMenuAndNoRouteOfTheAdministration()
    {
        var (name, password) = (Made("nova"), Made("senha-inicial").ToUpperInvariant());
        await using var admin = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(admin.Page, name, Annotation, password);
        await admin.Page.Locator("#cadastrar").ClickAsync();
        await Expect(admin.Page.Locator(".notice--success")).ToBeVisibleAsync();

        await using var entered = await browser.OpenAtAsync(people.Host.Address, "/entrada");
        await entered.Page.Locator("#entrada-nome").FillAsync(name);
        await entered.Page.Locator("#entrada-senha").FillAsync(password);
        await entered.Page.Locator("#entrar").ClickAsync();

        await Expect(entered.Page.Locator("#sair")).ToBeVisibleAsync();
        await Expect(entered.Page.Locator(".empty-state")).ToContainTextAsync("Nenhum domínio foi concedido a você");
        await Expect(entered.Page.Locator("nav")).ToHaveCountAsync(0);
        Assert.Equal(StatusNotFound, (await entered.Page.GotoAsync(List))?.Status);
        Assert.Equal(StatusNotFound, (await entered.Page.GotoAsync(Form))?.Status);
    }

    [Fact]
    public async Task Ca3ANameAlreadyUsedSendsTheFormBackWithTheErrorBoundToTheNameFieldTheStatusThatSaysItAndWhatWasTyped()
    {
        var typed = people.Host.Auditor;
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, typed, "Avaliação", Made("senha"));

        var response = await SubmitAsync(tab.Page);

        Assert.Equal(409, response.Status);
        await Expect(tab.Page).ToHaveURLAsync(new Regex("/pessoas/nova$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#pessoa-nome-erro")).ToHaveTextAsync("Já existe uma pessoa com esse nome de uso.");
        await Expect(tab.Page.Locator("#pessoa-nome-erro")).ToHaveAttributeAsync("role", "alert");
        await Expect(tab.Page.Locator("#pessoa-nome")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator("#pessoa-nome")).ToHaveAttributeAsync("aria-describedby", new Regex("pessoa-nome-erro", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#pessoa-nome")).ToHaveValueAsync(typed);
        await Expect(tab.Page.Locator("#pessoa-funcao")).ToHaveValueAsync("Avaliação");
        await Expect(tab.Page.Locator("#pessoa-senha")).ToHaveValueAsync(string.Empty);
        await Expect(tab.Page.Locator("#pessoa-funcao-erro, #pessoa-senha-erro")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca3ANameAlreadyUsedRegistersNobodyAndTheRegistryStillHasOnePersonWithThatNameAndItsOwnFunction()
    {
        var typed = people.Host.Curator;
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, typed, "Auditoria", Made("senha"));
        _ = await SubmitAsync(tab.Page);

        _ = await tab.Page.GotoAsync($"{List}?nome={typed}");

        Assert.Equal([[typed, "Curadoria do acervo", "Ativa"]], await RowsAsync(tab.Page));
    }

    [Theory]
    [InlineData("", "Anotação", "ZQX-x", "pessoa-nome", "O nome de uso não pode ser vazio nem ter espaço no começo ou no fim.")]
    [InlineData("zqx-com-espaco-no-fim ", "Anotação", "ZQX-x", "pessoa-nome", "O nome de uso não pode ser vazio nem ter espaço no começo ou no fim.")]
    [InlineData("zqx-sem-funcao", "", "ZQX-x", "pessoa-funcao", "não consta da política de acesso")]
    [InlineData("zqx-sem-senha", "Anotação", "", "pessoa-senha", "A senha não pode ser vazia.")]
    public async Task ABlankNameAFunctionNotChosenOrABlankPasswordSendsTheFormBackWithThatFieldInErrorAndTheRestAsTyped(string name, string function, string password, string field, string reason)
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, name, function, password);

        var response = await SubmitAsync(tab.Page);

        Assert.Equal(400, response.Status);
        await Expect(tab.Page.Locator($"#{field}-erro")).ToContainTextAsync(reason);
        await Expect(tab.Page.Locator($"#{field}")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator("[aria-invalid=true]")).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator("#pessoa-nome")).ToHaveValueAsync(name);
        await Expect(tab.Page.Locator("#pessoa-funcao")).ToHaveValueAsync(function);
        await Expect(tab.Page.Locator("#pessoa-senha")).ToHaveValueAsync(string.Empty);
        _ = await tab.Page.GotoAsync($"{List}?nome=zqx-sem");
        await Expect(tab.Page.Locator(".paged-list li")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheFormAsksForTheNameTheFunctionAndTheInitialPasswordWithTheFocusOnTheNameAndTheSixFunctionsOfThePolicyToChooseFrom()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Cadastrar pessoa", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("#pessoa-nome")).ToBeFocusedAsync();
        await Expect(tab.Page.Locator("#pessoa-senha")).ToHaveAttributeAsync("type", "password");
        await Expect(tab.Page.Locator("#pessoa-senha")).ToHaveAttributeAsync("autocomplete", "new-password");
        await Expect(tab.Page.Locator("main form")).ToHaveAttributeAsync("method", "post");
        await Expect(tab.Page.GetByLabel("Nome")).ToHaveCountAsync(1);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar à lista" })).ToHaveAttributeAsync("href", List);
        var choices = await tab.Page.Locator("#pessoa-funcao option").AllInnerTextsAsync();
        Assert.Equal(["Escolha a função", "Anotação", "Arbitragem", "Curadoria do acervo", "Avaliação", "Auditoria", "Administração técnica"], choices);
    }

    internal static async Task FillAsync(IPage page, string name, string function, string password)
    {
        await page.Locator("#pessoa-nome").FillAsync(name);
        _ = await page.Locator("#pessoa-funcao").SelectOptionAsync(function);
        await page.Locator("#pessoa-senha").FillAsync(password);
    }

    internal static Task<IResponse> SubmitAsync(IPage page) =>
        page.RunAndWaitForResponseAsync(() => page.Locator("#cadastrar").ClickAsync(), response => response.Request.Method == "POST");
}
