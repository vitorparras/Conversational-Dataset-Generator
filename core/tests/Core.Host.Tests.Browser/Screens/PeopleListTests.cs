using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The list of the people (issue 599, CA-2) in a real Chromium, over the host with the real gates, the real route and the real registry: each person with the name, the declared function and
/// the state in words, and nothing of any material; filtered by the part of the name, the function and the state, with the filter given back to its fields and kept in the address; and
/// the words for a filter that leaves nobody. The page has no violation of WCAG 2.1 A and AA in the light, in the dark, with less transparency and more contrast, reads over the glass,
/// fits the width of a phone, and is reached and filtered by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeopleListTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private const string Listed = "/pessoas?nome=zqx-lista-";

    [Fact]
    public async Task Ca2EachPersonShowsTheNameTheDeclaredFunctionAndTheStateInWordsAndNothingElse()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{List}?nome=zqx-lista-1");

        var rows = await RowsAsync(tab.Page);

        Assert.Equal(ListedWith("zqx-lista-1"), rows);
        Assert.Contains(rows, row => row[2] == "Inativa");
        Assert.Contains(rows, row => row[2] == "Ativa");
        await Expect(tab.Page.Locator("#lista-titulo")).ToHaveTextAsync($"{rows.Count} pessoas");
        await Expect(tab.Page.Locator(".paged-list a, .paged-list button, .paged-list input")).ToHaveCountAsync(0);
        Assert.All(await tab.Page.Locator(".paged-list li").AllInnerTextsAsync(), text => Assert.DoesNotMatch(@"\d{4}|senha|domínio|conteúdo", text));
    }

    [Fact]
    public async Task TheFunctionTheFormFiltersByAndTheAddressKeepsLeavesOnlyThePeopleOfThatFunction()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, List);
        var function = PeopleScreenHost.Listed[1].Function;

        await tab.Page.Locator("#filtro-nome").FillAsync("zqx-lista-");
        _ = await tab.Page.Locator("#filtro-funcao").SelectOptionAsync(function);
        await tab.Page.Locator("#filtrar").ClickAsync();

        var asked = Regex.Escape(Uri.EscapeDataString(function)).Replace("%20", @"(?:%20|[+])", StringComparison.Ordinal);
        await Expect(tab.Page).ToHaveURLAsync(new Regex($"[?&]funcao={asked}", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Equal(ListedWith("zqx-lista-").Where(row => row[1] == function), await RowsAsync(tab.Page));
        await Expect(tab.Page.Locator("#filtro-nome")).ToHaveValueAsync("zqx-lista-");
        await Expect(tab.Page.Locator("#filtro-funcao")).ToHaveValueAsync(function);
    }

    [Theory]
    [InlineData("ativas", "Ativa")]
    [InlineData("inativas", "Inativa")]
    public async Task TheStateTheFormFiltersByLeavesOnlyThePeopleInThatState(string state, string word)
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{Listed}&estado={state}");

        var rows = await RowsAsync(tab.Page);

        Assert.Equal(ListedWith("zqx-lista-").Where(row => row[2] == word).Take(20), rows);
        await Expect(tab.Page.Locator("#filtro-estado")).ToHaveValueAsync(state);
    }

    [Theory]
    [InlineData("ZQX-LISTA-4", 7)]
    [InlineData("zqx_lista", 0)]
    [InlineData("%25", 0)]
    [InlineData("zqx-lista-46", 1)]
    public async Task ThePartOfTheNameIsFoundWithoutRegardToCaseAndThePercentAndTheUnderscoreAreLiteral(string part, int found)
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{List}?nome={part}");

        Assert.Equal(found, (await RowsAsync(tab.Page)).Count);
        await Expect(tab.Page.Locator("#lista-titulo")).ToHaveTextAsync(found == 1 ? "1 pessoa" : $"{found} pessoas");
    }

    [Fact]
    public async Task AFilterThatLeavesNobodySaysSoAndHowToSeeTheOthersAndClearingItBringsTheListBack()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{List}?nome=zqx-ninguem-assim");

        await Expect(tab.Page.Locator(".empty-state")).ToContainTextAsync("Nenhuma pessoa com esse filtro.");
        await Expect(tab.Page.Locator(".empty-state")).ToContainTextAsync("Mude ou limpe o filtro para ver as outras.");
        await Expect(tab.Page.Locator("nav.pager")).ToHaveCountAsync(0);
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Limpar filtro" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex("/pessoas$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#filtro-nome")).ToHaveValueAsync(string.Empty);
        await Expect(tab.Page.Locator(".paged-list li")).Not.ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AFunctionThePolicyDoesNotAcceptInTheAddressIsLeftOutAndTheListIsNotEmptiedByIt()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{Listed}&funcao=Inexistente");

        await Expect(tab.Page.Locator("#lista-titulo")).ToHaveTextAsync($"{PeopleScreenHost.Listed.Count} pessoas");
        await Expect(tab.Page.Locator("#filtro-funcao")).ToHaveValueAsync(string.Empty);
    }

    [Fact]
    public async Task TheAdministrationSeesPeopleInTheMenuMarkedAsTheCurrentPageAndAnActionToRegisterOne()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, List);

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Pessoas", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("nav.nav").GetByRole(AriaRole.Link, new() { Name = "Pessoas" })).ToHaveAttributeAsync("aria-current", "page");
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Cadastrar pessoa" })).ToHaveAttributeAsync("href", Form);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheListHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Listed);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "pessoas");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheListThatLeavesNobodyHasNoViolationOfWcagAAndAa(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{List}?nome=zqx-ninguem-assim");
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "pessoas-vazia");
    }

    [Fact]
    public async Task TheListFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Listed);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Listed);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "pessoas");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheActionAndEachFieldOfTheFilterInOrderWithTheFocusSeenAndFiltersWithEnter()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, List);
        var order = new List<string>();
        var seen = new List<bool>();

        await tab.Page.Locator("#filtro-nome").FocusAsync();
        await tab.Page.Keyboard.PressAsync("Shift+Tab");
        order.Add(await tab.Page.EvaluateAsync<string>(Focused));
        await tab.Page.Locator("#filtro-nome").FocusAsync();
        for (var stop = 0; stop < 4; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            order.Add(await tab.Page.EvaluateAsync<string>(Focused));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(["Cadastrar pessoa", "filtro-funcao", "filtro-estado", "filtrar", "Limpar filtro"], order);
        Assert.DoesNotContain(false, seen);
        await tab.Page.Locator("#filtro-nome").FocusAsync();
        await tab.Page.Keyboard.TypeAsync("zqx-lista-0");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex("[?&]nome=zqx-lista-0", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Equal(ListedWith("zqx-lista-0"), await RowsAsync(tab.Page));
    }
}
