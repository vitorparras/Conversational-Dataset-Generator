using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Platform.Startup;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the state of the system (issue 604) in a real Chromium, over the host with the real gates and the real route: alive and fit apart, with each check of the startup and
/// its result (CA-1), the versions of the core, the service, the contract, the model and the schema (CA-2), and a person with neither the technical administration nor the audit
/// answered as for an address that does not exist (CA-3). A state is said in words, with a sign and a border of its own, and never by colour alone. The screen has no violation of WCAG
/// 2.1 A and AA in the light, in the dark, with less transparency and more contrast, and on the width of a phone, reads over the glass, and is reached by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SystemStateScreenTests(BrowserFixture browser, SystemHostFixture system) : IClassFixture<SystemHostFixture>
{
    private const string Route = "/estado-do-sistema";
    private const string Missing = "/zqx-nao-existe-6040";
    private const string ChecksRows = "[aria-labelledby=conferencias-titulo] tbody tr";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const string BorderOfFirst = "(tone) => { const s = getComputedStyle(document.querySelector('.mark--' + tone)); return s.borderTopStyle + ' ' + s.borderTopWidth; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int StatusNotFound = 404;

    [Fact]
    public async Task Ca1AliveAndFitAreSaidApartAndEachCheckOfTheStartupHasItsResult()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);

        await Expect(tab.Page.Locator("[aria-labelledby=agora-titulo]")).ToContainTextAsync("Vivo");
        await Expect(FactOf(tab.Page, "Vivo")).ToContainTextAsync("Sim: o sistema responde");
        await Expect(FactOf(tab.Page, "Apto")).ToContainTextAsync("Não: uma conferência da subida falhou");
        await Expect(tab.Page.Locator(ChecksRows)).ToHaveCountAsync(2);
        await Expect(tab.Page.Locator(ChecksRows).Nth(0)).ToContainTextAsync(SystemScreenHost.PassedCheck);
        await Expect(tab.Page.Locator(ChecksRows).Nth(0)).ToContainTextAsync("Passou");
        await Expect(tab.Page.Locator(ChecksRows).Nth(1)).ToContainTextAsync(SystemScreenHost.FailedCheck);
        await Expect(tab.Page.Locator(ChecksRows).Nth(1)).ToContainTextAsync("Falhou");
        await Expect(tab.Page.Locator(ChecksRows).Nth(1)).ToContainTextAsync($"{SystemScreenHost.FailedMessage}. Encontrado: {SystemScreenHost.FailedObserved}");
        await Expect(tab.Page.Locator("[aria-labelledby=conferencias-titulo]")).ToContainTextAsync("1 de 2 passaram");
    }

    [Fact]
    public async Task Ca1FitIsYesOnlyWhenEveryCheckPassed()
    {
        system.Host.Report = new StartupReport([CheckResult.Passed(SystemScreenHost.PassedCheck, "a chave foi lida e confere", "arquivo de chaves do servidor")], DateTimeOffset.UtcNow);
        try
        {
            await using var tab = await OpenAsync(system.Host.Administrator);

            await Expect(FactOf(tab.Page, "Apto")).ToContainTextAsync("Sim: pronto para trabalhar");
            await Expect(tab.Page.Locator("[aria-labelledby=conferencias-titulo]")).ToContainTextAsync("1 de 1 passaram");
        }
        finally
        {
            system.Host.Report = new StartupReport(
                [
                    CheckResult.Passed(SystemScreenHost.PassedCheck, "a chave foi lida e confere", "arquivo de chaves do servidor"),
                    CheckResult.Failed(SystemScreenHost.FailedCheck, SystemScreenHost.FailedMessage, "servidor de banco da instalação") with { Observed = SystemScreenHost.FailedObserved },
                ],
                DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public async Task Ca2TheVersionsOfTheCoreTheContractTheServiceTheModelAndTheSchemaAreShownAndWhatIsNotObtainedIsSaidSo()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);

        await Expect(FactOf(tab.Page, "Núcleo")).ToContainTextAsync("9.8.7");
        await Expect(FactOf(tab.Page, "Núcleo")).ToContainTextAsync("0123456789abcdef0123456789abcdef01234567");
        await Expect(FactOf(tab.Page, "Esquema do banco")).ToContainTextAsync("0042");
        await Expect(FactOf(tab.Page, "Serviço de linguagem").Last).ToContainTextAsync("Não obtida");
        await Expect(FactOf(tab.Page, "Contrato do serviço")).ToContainTextAsync("Não obtida");
        await Expect(FactOf(tab.Page, "Modelo")).ToContainTextAsync("Não obtida");
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    public async Task Ca3TheAdministrationAndTheAuditReachTheScreenAndItsEntryIsInTheMenu(string who)
    {
        var person = who == "Administrator" ? system.Host.Administrator : system.Host.Auditor;
        await using var tab = await OpenAsync(person);

        Assert.Equal(["Estado do sistema", "Configuração em vigor"], await tab.Page.Locator("nav .nav__link").AllInnerTextsAsync());
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Estado do sistema" })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Theory]
    [InlineData(Route)]
    [InlineData("/configuracao")]
    public async Task Ca3WhoHasNeitherTheTechnicalAdministrationNorTheAuditIsAnsweredAsForAnAddressThatDoesNotExist(string route)
    {
        Assert.NotNull(route);
        await using var tab = await SystemSession.SignedInAsync(browser, system.Host, system.Host.Annotator);

        var refused = await tab.Page.GotoAsync(route);
        var refusedPage = await tab.Page.ContentAsync();
        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();

        Assert.Equal((StatusNotFound, StatusNotFound), (refused?.Status, missing?.Status));
        Assert.Equal(missingPage.Replace(Missing.TrimStart('/'), string.Empty, StringComparison.Ordinal), refusedPage.Replace(route.TrimStart('/'), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("Estado do sistema", refusedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Configuração em vigor", refusedPage, StringComparison.Ordinal);
        Assert.DoesNotContain(system.Host.Secret, refusedPage, StringComparison.Ordinal);
        Assert.Empty(await tab.Page.Locator("nav .nav__link").AllInnerTextsAsync());
    }

    [Fact]
    public async Task AStateIsSaidInWordsWithASignAndABorderOfItsOwnAndNeverByColourAlone()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);

        var borders = new[] { "good", "bad", "waiting" };
        var styles = new List<string>();
        foreach (var tone in borders)
        {
            styles.Add(await tab.Page.EvaluateAsync<string>(BorderOfFirst, tone));
        }

        Assert.Equal(borders.Length, styles.Distinct().Count());
        var marks = tab.Page.Locator(".mark");
        Assert.True(await marks.CountAsync() > 0);
        foreach (var mark in await marks.AllAsync())
        {
            Assert.NotEmpty((await mark.InnerTextAsync()).Replace("✓", string.Empty, StringComparison.Ordinal).Replace("✕", string.Empty, StringComparison.Ordinal).Trim());
            await Expect(mark.Locator(".mark__shape")).ToHaveAttributeAsync("aria-hidden", "true");
        }
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheScreenHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(system.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "estado-do-sistema");
    }

    [Fact]
    public async Task TheScreenFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "estado-do-sistema");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheHeaderTheMenuAndTheTableToScrollInOrderWithTheFocusSeen()
    {
        await using var tab = await OpenAsync(system.Host.Auditor);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < HeaderStops.Count + 3; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(HeaderStops.Then("nav__link", "nav__link", "data-table"), reached);
        Assert.DoesNotContain(false, seen);
    }

    /// <summary>The detail of a term of the lists of facts, found by the term and not by the markup that holds it.</summary>
    private static ILocator FactOf(IPage page, string term) => page.Locator($"dl > div:has(dt:text-is('{term}')) dd");

    private async Task<BrowserTab> OpenAsync(string name)
    {
        var tab = await SystemSession.SignedInAsync(browser, system.Host, name);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Estado do sistema", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
