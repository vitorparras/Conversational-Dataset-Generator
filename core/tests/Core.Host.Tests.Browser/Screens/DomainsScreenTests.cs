using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the domains (issue 602) in a real Chromium, over the host with the real gates and the real route, once with two domains and once with none: each domain with its
/// version of the taxonomy and of the dimensions, said as none loaded where the model keeps none (CA-1); no domain, said so, with the command of the server that makes the first (CA-2);
/// and whoever has neither the technical administration nor a domain granted to the curation answered as for an address that does not exist (CA-3). The screen has no violation of WCAG
/// 2.1 A and AA in the light, in the dark, with less transparency and more contrast, and on the width of a phone, reads over the glass, and is reached by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class DomainsScreenTests(BrowserFixture browser, DomainsHostFixture withDomains, NoDomainsHostFixture withNone)
    : IClassFixture<DomainsHostFixture>, IClassFixture<NoDomainsHostFixture>
{
    private const string Route = "/dominios";
    private const string Missing = "/zqx-nao-existe-6020";
    private const string Rows = "[aria-labelledby=dominios-titulo] tbody tr";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int StatusNotFound = 404;
    private const int StatusOk = 200;

    [Fact]
    public async Task Ca1EachDomainIsListedInTheOrderItWasCreatedWithTheVersionOfTheTaxonomyAndOfTheDimensions()
    {
        await using var tab = await OpenAsync(withDomains.Host, withDomains.Host.Administrator);

        Assert.Equal(DomainScreenHost.Domains, await tab.Page.Locator($"{Rows} th[scope=row]").AllInnerTextsAsync());
        Assert.Equal(["Domínio", "Criado em", "Versão da taxonomia", "Versão das dimensões"], await tab.Page.Locator("[aria-labelledby=dominios-titulo] thead th").AllInnerTextsAsync());
        await Expect(tab.Page.Locator("#dominios-tabela-legenda")).ToHaveTextAsync("2 domínios, na ordem em que foram criados.");
        Assert.All(await tab.Page.Locator($"{Rows} td:nth-child(2)").AllInnerTextsAsync(), when => Assert.NotEmpty(when.Trim()));
    }

    [Fact]
    public async Task Ca1AVersionTheModelDoesNotKeepIsSaidAsNoneLoadedNeverMadeUp()
    {
        await using var tab = await OpenAsync(withDomains.Host, withDomains.Host.Administrator);

        var versions = await tab.Page.Locator($"{Rows} td:nth-child(3), {Rows} td:nth-child(4)").AllInnerTextsAsync();

        Assert.Equal(4, versions.Count);
        Assert.All(versions, text => Assert.Contains("Nenhuma carregada", text, StringComparison.Ordinal));
        await Expect(tab.Page.Locator("main form, main input, main textarea, main select, main button")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca2WithNoDomainThePageSaysSoAndPointsToTheCommandOfTheServerThatMakesTheFirst()
    {
        await using var tab = await OpenAsync(withNone.Host, withNone.Host.Administrator);

        await Expect(tab.Page.Locator(".empty-state")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum domínio");
        await Expect(tab.Page.Locator(".empty-state code")).ToHaveTextAsync("create-domain");
        await Expect(tab.Page.Locator("table")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".notice--error, [role=alert]")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca2ThePageWithNoDomainHasNoViolationOfWcagAAndAa(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(withNone.Host, withNone.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "dominios-nenhum");
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    [InlineData("Curator")]
    [InlineData("Annotator")]
    public async Task Ca3OnlyTheTechnicalAdministrationReachesTheScreenAndEveryoneElseIsAnsweredAsForAnAddressThatDoesNotExist(string who)
    {
        var host = withDomains.Host;
        var person = who switch
        {
            "Administrator" => host.Administrator,
            "Auditor" => host.Auditor,
            "Curator" => host.Curator,
            _ => host.Annotator,
        };
        await using var tab = await ScreenSession.SignedInAsync(browser, host, person);

        var answer = await tab.Page.GotoAsync(Route);
        var page = await tab.Page.ContentAsync();
        if (who == "Administrator")
        {
            Assert.Equal(StatusOk, answer?.Status);
            await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Domínios" })).ToHaveAttributeAsync("aria-current", "page");
            return;
        }

        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();
        Assert.Equal((StatusNotFound, StatusNotFound), (answer?.Status, missing?.Status));
        Assert.Equal(missingPage.Replace(Missing.TrimStart('/'), string.Empty, StringComparison.Ordinal), page.Replace(Route.TrimStart('/'), string.Empty, StringComparison.Ordinal));
        Assert.All(DomainScreenHost.Domains.Append("Domínios").Append("create-domain"), text => Assert.DoesNotContain(text, page, StringComparison.Ordinal));
        Assert.Empty(await tab.Page.Locator("nav .nav__link").AllInnerTextsAsync());
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheScreenHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(withDomains.Host, withDomains.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "dominios");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThePagesFitThePhoneWidthWithoutScrollingSidewaysAndHaveNoViolation(bool listed)
    {
        var host = listed ? withDomains.Host : withNone.Host;
        await using var tab = await OpenAsync(host, host.Administrator);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", listed ? "dominios" : "dominios-nenhum");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheHeaderTheMenuAndTheTableToScrollInOrderWithTheFocusSeen()
    {
        await using var tab = await OpenAsync(withDomains.Host, withDomains.Host.Administrator);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < HeaderStops.Count + 2; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(HeaderStops.Then("nav__link", HeaderStops.TableStop), reached);
        Assert.DoesNotContain(false, seen);
    }

    private async Task<BrowserTab> OpenAsync(ScreenHostBase host, string name)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, name);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Domínios", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
