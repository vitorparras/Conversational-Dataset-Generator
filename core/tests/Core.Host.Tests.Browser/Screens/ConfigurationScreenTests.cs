using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the configuration in force (issue 603) in a real Chromium, over the host with the real gates and the real route: every key with the origin that won (CA-1), a secret
/// key with its name and origin and never its value, neither on the page nor in the answer (CA-2), and nothing to edit, each value saying it changes on the server (CA-3). The screen
/// has no violation of WCAG 2.1 A and AA in the light, in the dark, with less transparency and more contrast, and on the width of a phone, reads over the glass, and is reached by the
/// keyboard alone with the focus seen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ConfigurationScreenTests(BrowserFixture browser, SystemHostFixture system) : IClassFixture<SystemHostFixture>
{
    private const string Route = "/configuracao";
    private const string SecretKey = "Custody:Holder";
    private const string Rows = "[aria-labelledby=chaves-titulo] tbody tr:not(:has(th[scope=rowgroup]))";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int StatusOk = 200;

    [Fact]
    public async Task Ca1EveryKeyInForceIsOnThePageWithTheOriginThatWonForIt()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);

        var keys = await tab.Page.Locator($"{Rows} th[scope=row] code").AllInnerTextsAsync();

        Assert.Equal([.. system.Host.Configuration.Entries.Select(entry => entry.Key)], keys);
        await Expect(tab.Page.Locator($"{Rows}:has(code:text-is('{SecretKey}')) td").Nth(1)).ToHaveTextAsync("Argumento da linha de comando");
        await Expect(tab.Page.Locator($"{Rows}:has(code:text-is('Host:Urls')) td").Nth(1)).ToHaveTextAsync("Padrão do sistema");
        await Expect(tab.Page.Locator($"{Rows}:has(code:text-is('Custody:LastRecoveryTest')) td").First).ToHaveTextAsync(SystemScreenHost.DeclaredDate);
    }

    [Fact]
    public async Task Ca2TheSecretKeyShowsItsNameAndOriginAndNeverItsValueNotInThePageNorInTheAnswer()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);
        var address = new Uri(new Uri(tab.Page.Url), Route).AbsoluteUri;

        var answer = await tab.Page.Context.APIRequest.GetAsync(address, new() { Headers = new Dictionary<string, string> { ["Accept"] = "text/html" } });
        var body = await answer.TextAsync();
        var shown = await tab.Page.Locator("html").InnerHTMLAsync();
        var text = await tab.Page.Locator("body").InnerTextAsync();

        Assert.Equal(StatusOk, answer.Status);
        Assert.All([body, shown, text], page => Assert.DoesNotContain(system.Host.Secret, page, StringComparison.Ordinal));
        var row = tab.Page.Locator($"{Rows}:has(code:text-is('{SecretKey}'))");
        await Expect(row.Locator("td").First).ToContainTextAsync("Segredo, valor oculto");
        await Expect(row.Locator("td").Nth(1)).ToHaveTextAsync("Argumento da linha de comando");
    }

    [Fact]
    public async Task Ca3NothingOnThePageEditsAValueAndEveryValueSaysItChangesOnTheServer()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);

        await Expect(tab.Page.Locator("main form, main input, main textarea, main select, main button")).ToHaveCountAsync(0);
        var where = await tab.Page.Locator($"{Rows} td:nth-child(4)").AllInnerTextsAsync();
        Assert.Equal(system.Host.Configuration.Entries.Count, where.Count);
        Assert.All(where, text => Assert.Equal("No servidor", text));
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
        await ModeCapture.SaveAsync(tab.Page, mode, "configuracao");
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
        await ModeCapture.SaveAsync(tab.Page, "celular", "configuracao");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheHeaderTheMenuAndTheTableToScrollInOrderWithTheFocusSeen()
    {
        await using var tab = await OpenAsync(system.Host.Administrator);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < 6; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(["skip-link", "header__name", "sair", "nav__link", "nav__link", "data-table"], reached);
        Assert.DoesNotContain(false, seen);
        await Expect(tab.Page.Locator("#chaves-tabela-legenda")).ToBeVisibleAsync();
    }

    private async Task<BrowserTab> OpenAsync(string name)
    {
        var tab = await SystemSession.SignedInAsync(browser, system.Host, name);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Configuração em vigor", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
