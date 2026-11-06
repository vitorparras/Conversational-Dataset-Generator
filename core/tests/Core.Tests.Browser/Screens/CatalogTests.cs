using Core.Tests.Browser.Accessibility;
using Core.Tests.Browser.Fixtures;
using Core.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Tests.Browser.Screens;

/// <summary>
/// The catalog page of the design system (issue 658, CA-4): each component in each state, no violation of WCAG 2.1 A and AA in any setting of the system,
/// the text over the background as it is drawn at AA (CA-3), and the states told without colour.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CatalogTests(BrowserFixture browser)
{
    private const string Absent = "(pairs) => pairs.filter(([name, selector]) => document.querySelector(selector) === null).map(([name]) => name)";
    private const string Kinds = "() => [...document.querySelectorAll('.notice')].map(n => n.querySelector('.notice__kind').textContent + ' ' + n.querySelector('.notice__mark').textContent)";

    /// <summary>The components the issue lists, each state an example the page must show; the selector is how the production markup names it.</summary>
    private static readonly string[][] Examples =
    [
        ["cabecalho", "header.header a.header__name"],
        ["botao-principal", ".button--primary:not([disabled])"],
        ["botao-secundario", ".button--secondary"],
        ["botao-discreto", ".button--quiet"],
        ["botao-de-perigo", ".button--danger"],
        ["botao-desabilitado", "button.button[disabled]"],
        ["botao-com-atalho", "[data-shortcut] .button__key"],
        ["botao-como-link", "a.button[href]"],
        ["campo", ".field__label + .field__input"],
        ["campo-com-dica", ".field__hint"],
        ["campo-obrigatorio", ".field__input[aria-required=true]"],
        ["campo-com-erro", ".field__input[aria-invalid=true][aria-describedby] ~ .field__error[role=alert]"],
        ["mensagem-de-aviso", ".notice--info"],
        ["mensagem-de-conclusao", ".notice--success"],
        ["mensagem-de-pendencia", ".notice--pending"],
        ["mensagem-de-erro", ".notice--error[role=alert]"],
        ["painel-de-vidro", ".panel"],
        ["lista-com-itens", "ul.paged-list li"],
        ["lista-com-paginas", "nav.pager a[rel=next]"],
        ["lista-vazia", ".paged-list__empty[role=status]"],
    ];

    [Fact]
    public async Task Ca4TheCatalogShowsEveryComponentInEveryState()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);

        var absent = await tab.Page.EvaluateAsync<string[]>(Absent, Examples);

        Assert.Empty(absent);
    }

    [Fact]
    public async Task Ca4TheStatesOfAMessageDifferByTheWordAndByTheShapeAndNotOnlyByColour()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);

        var kinds = await tab.Page.EvaluateAsync<string[]>(Kinds);

        Assert.Equal(4, kinds.Length);
        Assert.Equal(kinds.Length, kinds.Distinct().Count());
        Assert.Equal(kinds.Length, kinds.Select(kind => kind.Split(' ')[0]).Distinct().Count());
        Assert.Equal(kinds.Length, kinds.Select(kind => kind.Split(' ')[1]).Distinct().Count());
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca4TheCatalogHasNoViolationOfWcagAAndAaInTheSettingOfTheSystem(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "catalogo");
    }

    [Fact]
    public async Task Ca4TheCatalogFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await tab.Page.SetViewportSizeAsync(390, 844);

        var overflow = await tab.Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "catalogo");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca3TheTextOfTheCatalogReachesAaOverTheBackgroundAsItIsDrawn(string name)
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await MediaMode.Named(name).ApplyAsync(tab.Page);

        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task Ca3TheLinkToSkipToTheContentReachesAaOverTheHeaderWhenItTakesTheFocus(string name)
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Catalog);
        await MediaMode.Named(name).ApplyAsync(tab.Page);
        await tab.Page.Keyboard.PressAsync("Tab");
        await Expect(tab.Page.Locator(".skip-link")).ToBeFocusedAsync();

        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }
}
