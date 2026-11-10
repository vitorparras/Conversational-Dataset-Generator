using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the chain of custody (issue 605) in a real Chromium, over the host with the real gates, the real routes and a real verification, once with the chain intact and once with
/// an event changed outside the system: the instant, the scope and the count of what was verified (CA-1); the first event that does not conform and the warning that the domain it affects
/// only accepts reading (CA-2); and no way, on the screen or beside it, to rebuild the chain (CA-3). The screen has no violation of WCAG 2.1 A and AA in the light, in the dark, with less
/// transparency and more contrast, and on the width of a phone, reads over the glass, and is reached by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class ChainScreenTests(BrowserFixture browser, IntactChainHostFixture intact, BrokenChainHostFixture broken)
    : IClassFixture<IntactChainHostFixture>, IClassFixture<BrokenChainHostFixture>
{
    private const string Route = "/custodia";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";
    private const string FocusedText = "() => document.activeElement.textContent.trim()";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;

    [Fact]
    public async Task Ca1TheIntactChainShowsThatItPassedWhenItWasVerifiedAndWhatItCoversWithTheCountOfEvents()
    {
        await using var tab = await OpenAsync(intact.Host, intact.Host.Administrator);

        await Expect(FactOf(tab.Page, "Resultado")).ToContainTextAsync("Passou: todos os elos conferem");
        await Expect(FactOf(tab.Page, "Escopo")).ToContainTextAsync("A cadeia inteira:");
        await Expect(FactOf(tab.Page, "Escopo")).ToHaveTextAsync(EventsRead());
        await Expect(FactOf(tab.Page, "Quando").Locator("time")).ToHaveCountAsync(1);
        await Expect(FactOf(tab.Page, "Pedida por")).ToHaveTextAsync("O sistema, no intervalo em vigor");
        await Expect(tab.Page.GetByText("Aberto desde o evento")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".notice--error, [role=alert]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca2TheBrokenChainPointsToTheFirstEventThatDoesNotConformAndSaysTheDomainItAffectsOnlyAcceptsReading()
    {
        await using var tab = await OpenAsync(broken.Host, broken.Host.Administrator);

        await Expect(FactOf(tab.Page, "Resultado")).ToContainTextAsync("Quebrada: um elo não confere");
        await Expect(FactOf(tab.Page, "Incidente")).ToContainTextAsync($"Aberto desde o evento nº {ChainScreenHost.BrokenSequence}, o primeiro que não confere");
        await Expect(FactOf(tab.Page, "Incidente")).ToContainTextAsync("o domínio afetado só aceita leitura");
        await Expect(tab.Page.Locator("[aria-labelledby=historico-titulo] tbody tr").First).ToContainTextAsync($"Quebrada no evento nº {ChainScreenHost.BrokenSequence}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ca3NothingOnTheScreenOffersAWayToRebuildTheChainAndTheOnlyStepItOffersReadsAndRecords(bool isBroken)
    {
        var host = isBroken ? broken.Host : intact.Host;
        await using var tab = await OpenAsync(host, host.Administrator);

        await Expect(tab.Page.Locator("main form, main input, main textarea, main select, main button")).ToHaveCountAsync(0);
        var links = await tab.Page.Locator("main a").EvaluateAllAsync<string[]>("(links) => links.map((link) => new URL(link.href).pathname)");
        Assert.Equal(["/custodia/verificar"], links);
        var words = await tab.Page.Locator("main a, main button, main summary").AllInnerTextsAsync();
        Assert.All(words, text => Assert.DoesNotMatch(Rebuild(), text));
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("Nada nesta tela refaz a cadeia.");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheScreenOfAnIntactChainHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(intact.Host, intact.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "custodia-integra");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheScreenOfABrokenChainHasNoViolationOfWcagAAndAaAndItsIncidentReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(broken.Host, broken.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "custodia-quebrada");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheScreenFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation(bool isBroken)
    {
        var host = isBroken ? broken.Host : intact.Host;
        await using var tab = await OpenAsync(host, host.Administrator);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", isBroken ? "custodia-quebrada" : "custodia-integra");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheHeaderTheMenuTheRequestAndTheHistoryInOrderWithTheFocusSeen()
    {
        await using var tab = await OpenAsync(intact.Host, intact.Host.Auditor);
        var expected = HeaderStops.Then("Custódia", "Verificar a cadeia agora", HeaderStops.TableStop);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < expected.Length; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            var byText = stop >= HeaderStops.Count && stop < expected.Length - 1;
            reached.Add(await tab.Page.EvaluateAsync<string>(byText ? FocusedText : FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(expected, reached);
        Assert.DoesNotContain(false, seen);
    }

    private static ILocator FactOf(IPage page, string term) => page.Locator($"dl > div:has(dt:text-is('{term}')) dd");

    [GeneratedRegex("refaz|refeit|reconstr|recalcul|repar|recri|apag|exclu|restaur", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Rebuild();

    [GeneratedRegex(@"^A cadeia inteira: \d+(\.\d{3})* eventos lidos$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EventsRead();

    private async Task<BrowserTab> OpenAsync(ScreenHostBase host, string name)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, name);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Custódia", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
