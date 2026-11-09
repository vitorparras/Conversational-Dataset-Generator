using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The verification of the chain asked for now (issue 605, CA-4, decision 0051) in a real Chromium, over the host with the real gates, the real routes and a real verification: the
/// confirmation on a page of its own that says what it reads, what it records and what it does not change; the result that enters the history with the name of who asked; a second
/// request of the same session within the window refused with the seconds to wait; and whoever has neither the technical administration nor the audit answered, on the page and on the
/// post, as for an address that does not exist. The page of the confirmation has no violation of WCAG 2.1 A and AA in the light, in the dark and on the width of a phone, and is
/// reached and sent by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ChainCheckTests(BrowserFixture browser, IntactChainHostFixture intact, BrokenChainHostFixture broken)
    : IClassFixture<IntactChainHostFixture>, IClassFixture<BrokenChainHostFixture>
{
    private const string Chain = "/custodia";
    private const string Check = "/custodia/verificar";
    private const string Missing = "/zqx-nao-existe-6050";
    private const string FirstRow = "[aria-labelledby=historico-titulo] tbody tr";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int StatusNotFound = 404;

    [Fact]
    public async Task Ca4TheConfirmationSaysWhatTheVerificationReadsRecordsAndDoesNotChangeBeforeAnythingIsAsked()
    {
        await using var tab = await OpenCheckAsync(intact.Host, intact.Host.Administrator);

        await Expect(FactOf(tab.Page, "O que se lê")).ToContainTextAsync("A cadeia de custódia inteira");
        await Expect(FactOf(tab.Page, "O que se grava")).ToContainTextAsync("com o seu nome como quem pediu");
        await Expect(FactOf(tab.Page, "O que não muda")).ToContainTextAsync("a cadeia não é refeita");
        await Expect(tab.Page.Locator("main form")).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator("main form")).ToHaveAttributeAsync("method", "post");
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar sem verificar" })).ToHaveAttributeAsync("href", Chain);
        await Expect(tab.Page.Locator("#verificar-espera")).ToBeAttachedAsync();
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    public async Task Ca4TheResultOfAVerificationAskedForNowEntersTheHistoryWithTheNameOfWhoAsked(string who)
    {
        var person = who == "Administrator" ? intact.Host.Administrator : intact.Host.Auditor;
        await using var tab = await OpenCheckAsync(intact.Host, person);
        var before = await tab.Page.Context.APIRequest.GetAsync(new Uri(new Uri(tab.Page.Url), Chain).AbsoluteUri, new() { Headers = Html() });
        var rowsBefore = Rows(await before.TextAsync());

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Verificar agora" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/custodia\?pedida$"));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("A verificação que você pediu terminou");
        await Expect(tab.Page.Locator(FirstRow)).ToHaveCountAsync(rowsBefore + 1);
        await Expect(tab.Page.Locator(FirstRow).First.Locator("td").Nth(2)).ToHaveTextAsync(person);
        await Expect(tab.Page.Locator(FirstRow).First.Locator("td").First).ToContainTextAsync("Passou");
        await Expect(FactOf(tab.Page, "Pedida por")).ToHaveTextAsync(person);
    }

    [Fact]
    public async Task Ca4ASecondRequestOfTheSameSessionWithinTheWindowIsRefusedWithTheSecondsToWaitAndRecordsNothing()
    {
        await using var tab = await OpenCheckAsync(intact.Host, intact.Host.Administrator);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Verificar agora" }).ClickAsync();
        await Expect(tab.Page.Locator(".notice--success")).ToBeVisibleAsync();
        var rows = await tab.Page.Locator(FirstRow).CountAsync();

        _ = await tab.Page.GotoAsync(Check);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Verificar agora" }).ClickAsync();

        await Expect(tab.Page.Locator(".notice--pending")).ToContainTextAsync("Você pediu uma verificação há pouco. Espere");
        await Expect(tab.Page.Locator(".notice--pending")).ToContainTextAsync("segundos e peça de novo.");
        await Expect(tab.Page.Locator(".notice--pending .notice__kind")).ToHaveTextAsync("Pendente:");
        await Expect(tab.Page.Locator(".notice--error")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Verificar a cadeia agora", Level = 1 })).ToBeVisibleAsync();
        _ = await tab.Page.GotoAsync(Chain);
        await Expect(tab.Page.Locator(FirstRow)).ToHaveCountAsync(rows);
    }

    [Fact]
    public async Task Ca4AVerificationAskedForOnABrokenChainRecordsTheBreakAgainAndDoesNotRepairIt()
    {
        await using var tab = await OpenCheckAsync(broken.Host, broken.Host.Administrator);

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Verificar agora" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/custodia\?pedida$"));
        await Expect(FactOf(tab.Page, "Resultado")).ToContainTextAsync("Quebrada: um elo não confere");
        await Expect(FactOf(tab.Page, "Incidente")).ToContainTextAsync($"evento nº {ChainScreenHost.BrokenSequence}");
        await Expect(FactOf(tab.Page, "Pedida por")).ToHaveTextAsync(broken.Host.Administrator);
    }

    [Theory]
    [InlineData("Curator", Chain)]
    [InlineData("Annotator", Chain)]
    [InlineData("Curator", Check)]
    [InlineData("Annotator", Check)]
    public async Task Ca4WhoHasNeitherTheTechnicalAdministrationNorTheAuditIsAnsweredAsForAnAddressThatDoesNotExistOnThePageAndOnThePost(string who, string route)
    {
        Assert.NotNull(route);
        var host = intact.Host;
        await using var tab = await ScreenSession.SignedInAsync(browser, host, who == "Curator" ? host.Curator : host.Annotator);

        var refused = await tab.Page.GotoAsync(route);
        var refusedPage = await tab.Page.ContentAsync();
        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();
        var posted = await tab.Page.Context.APIRequest.PostAsync(new Uri(new Uri(tab.Page.Url), Check).AbsoluteUri, new() { Headers = Html(), Form = tab.Page.Context.APIRequest.CreateFormData() });

        Assert.Equal((StatusNotFound, StatusNotFound, StatusNotFound), (refused?.Status, missing?.Status, posted.Status));
        Assert.Equal(missingPage.Replace(Missing.TrimStart('/'), string.Empty, StringComparison.Ordinal), refusedPage.Replace(route.TrimStart('/'), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("Custódia", refusedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("verificação", refusedPage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheConfirmationHasNoViolationOfWcagAAndAaAndReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenCheckAsync(intact.Host, intact.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "custodia-confirmar");
    }

    [Fact]
    public async Task TheConfirmationFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation()
    {
        await using var tab = await OpenCheckAsync(intact.Host, intact.Host.Administrator);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Check);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "custodia-confirmar");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheConfirmationAndSendsItWithTheFocusSeen()
    {
        await using var tab = await SignedAtAsync(intact.Host, intact.Host.Administrator, Chain);

        for (var stop = 0; stop < HeaderStops.Count + 2 && !await tab.Page.EvaluateAsync<bool>("() => document.activeElement.textContent.trim() === 'Pedir uma verificação agora'"); stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
        }

        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Verificar a cadeia agora", Level = 1 })).ToBeVisibleAsync();
        for (var stop = 0; stop < HeaderStops.Count + 3 && !await tab.Page.EvaluateAsync<bool>("() => document.activeElement.id === 'verificar-agora'"); stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
        }

        Assert.True(await tab.Page.EvaluateAsync<bool>("() => document.activeElement.matches(':focus-visible') && getComputedStyle(document.activeElement).outlineStyle !== 'none'"));
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/custodia\?pedida$"));
    }

    private static ILocator FactOf(IPage page, string term) => page.Locator($"dl > div:has(dt:text-is('{term}')) dd");

    private static Dictionary<string, string> Html() => new() { ["Accept"] = "text/html" };

    private static int Rows(string page) => Regex.Count(page, "<tr", RegexOptions.None, TimeSpan.FromSeconds(1)) - 1;

    private Task<BrowserTab> OpenCheckAsync(ScreenHostBase host, string name) => SignedAtAsync(host, name, Check);

    private async Task<BrowserTab> SignedAtAsync(ScreenHostBase host, string name, string route)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, name);
        try
        {
            _ = await tab.Page.GotoAsync(route);
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
