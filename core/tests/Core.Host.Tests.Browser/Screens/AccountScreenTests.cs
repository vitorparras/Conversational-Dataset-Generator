using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The own account (issues 597 and 598) in a real Chromium, over the host with the real gates, the real routes and the real store of sessions: the open sessions of the one who entered, each
/// with its start and its last use and the current one marked (CA-1 of 598); nothing of the network or of the browser, and an identifier that is never the one of the session (CA-2); and an
/// address that names no session of the list, or the current one, answered as for an address that does not exist. The page has no violation of WCAG 2.1 A and AA in the light, in the dark,
/// with less transparency and more contrast, and on the width of a phone, reads over the glass, and is reached by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class AccountScreenTests(BrowserFixture browser, AccountHostFixture account) : IClassFixture<AccountHostFixture>
{
    private const string Route = "/minha-conta";
    private const string End = "/minha-conta/sessoes/encerrar";
    private const string EndOthers = "/minha-conta/sessoes/encerrar-outras";
    private const string Missing = "/zqx-nao-existe-5970";
    private const string Rows = "[aria-labelledby=sessoes-titulo] tbody tr";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string Focused = "() => `${document.activeElement.id}|${document.activeElement.className}|${document.activeElement.textContent.trim()}`";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int StatusNotFound = 404;

    private static readonly string[] OfTheNetworkAndOfTheBrowser = ["127.0.0.1", "::1", "LOCALHOST", "CHROME", "CHROMIUM", "MOZILLA", "HEADLESS", "PLAYWRIGHT", "USER-AGENT", "ENDEREÇO", "NAVEGADOR"];
    private static readonly string[] Ways = ["Trocar senha", "Encerrar a sessão iniciada em", "Encerrar todas as outras"];

    /// <summary>The stops of the page after the header: the password link, the link that ends the other session, the table the keyboard scrolls and the button that ends the others.</summary>
    private const int PageStops = 4;

    [Fact]
    public async Task Ca1EachOpenSessionShowsItsStartAndItsLastUseAndTheCurrentOneIsMarked()
    {
        await using var tabs = await TwoSessionsAsync();

        var rows = tabs.Current.Page.Locator(Rows);
        await Expect(rows).ToHaveCountAsync(2);
        Assert.Equal(["Início", "Última atividade", "Ação"], await tabs.Current.Page.Locator("[aria-labelledby=sessoes-titulo] thead th").AllInnerTextsAsync());
        await Expect(rows.Locator("th time")).ToHaveCountAsync(2);
        await Expect(rows.Locator("td:nth-child(2) time")).ToHaveCountAsync(2);
        await Expect(rows.Filter(new() { HasText = "Esta sessão" })).ToHaveCountAsync(1);
        await Expect(rows.Filter(new() { HasText = "Esta sessão" }).Locator("a")).ToHaveCountAsync(0);
        await Expect(rows.Filter(new() { HasNotText = "Esta sessão" }).GetByRole(AriaRole.Link, new() { Name = "Encerrar" })).ToHaveCountAsync(1);
        await Expect(tabs.Current.Page.Locator("#sessoes-tabela-legenda")).ToHaveTextAsync("2 sessões abertas, contando esta. Horários em UTC.");
        await Expect(tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Encerrar todas as outras" })).ToHaveAttributeAsync("href", EndOthers);
    }

    [Fact]
    public async Task Ca1WithOnlyTheCurrentSessionThePageSaysSoAndOffersNothingToEnd()
    {
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());

        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator(Rows)).ToContainTextAsync("Esta sessão");
        await Expect(tab.Page.Locator("#sessoes-tabela-legenda")).ToHaveTextAsync("Só esta sessão está aberta.");
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Encerrar todas as outras" })).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator($"{Rows} a")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca2TheListHasNoAddressOfTheNetworkNoNameOfTheBrowserAndNoIdentifierOfTheSession()
    {
        await using var tabs = await TwoSessionsAsync();

        var page = (await tabs.Current.Page.ContentAsync()).ToUpperInvariant();
        var identifiers = (await tabs.Current.Page.Context.CookiesAsync()).Concat(await tabs.Other.Page.Context.CookiesAsync()).Select(cookie => cookie.Value).ToList();
        var listed = await tabs.Current.Page.Locator($"{Rows} a").EvaluateAllAsync<string[]>("(links) => links.map((link) => new URL(link.href).searchParams.get('sessao'))");

        Assert.All(OfTheNetworkAndOfTheBrowser, text => Assert.DoesNotContain(text, page, StringComparison.Ordinal));
        Assert.NotEmpty(identifiers);
        Assert.NotEmpty(listed);
        Assert.All(identifiers, identifier => Assert.DoesNotContain(identifier.ToUpperInvariant(), page, StringComparison.Ordinal));
        Assert.All(listed, one => Assert.DoesNotContain(one, identifiers));
    }

    [Fact]
    public async Task Ca3AnAddressThatNamesNoSessionOfTheListOrTheCurrentOneAnswersNotFoundOnThePageAndOnThePost()
    {
        await using var tabs = await TwoSessionsAsync();
        _ = await tabs.Other.Page.GotoAsync(Route);
        var ofTheCurrent = await tabs.Other.Page.Locator($"{Rows} a").First.GetAttributeAsync("href");
        Assert.NotNull(ofTheCurrent);
        var page = tabs.Current.Page;

        var absent = await page.GotoAsync($"{End}?sessao=ZQX-nao-existe");
        var none = await page.GotoAsync(End);
        var current = await page.GotoAsync(ofTheCurrent);
        var posted = await page.Context.APIRequest.PostAsync(
            new Uri(new Uri(page.Url), End).AbsoluteUri,
            new() { Headers = new Dictionary<string, string> { ["Accept"] = "text/html" }, Form = page.Context.APIRequest.CreateFormData().Set("sessao", "ZQX-nao-existe") });
        var listingOfTheCurrent = System.Web.HttpUtility.ParseQueryString(new Uri(new Uri(page.Url), ofTheCurrent).Query)["sessao"];
        var postedCurrent = await page.Context.APIRequest.PostAsync(
            new Uri(new Uri(page.Url), End).AbsoluteUri,
            new() { Headers = new Dictionary<string, string> { ["Accept"] = "text/html" }, Form = page.Context.APIRequest.CreateFormData().Set("sessao", listingOfTheCurrent!) });

        Assert.Equal([StatusNotFound, StatusNotFound, StatusNotFound, StatusNotFound, StatusNotFound], [absent?.Status, none?.Status, current?.Status, posted.Status, postedCurrent.Status]);
        _ = await tabs.Other.Page.GotoAsync(Route);
        await Expect(tabs.Other.Page.Locator(Rows)).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task Ca3TheAnswerForASessionNotInTheListIsTheSamePageAsForAnAddressThatDoesNotExist()
    {
        await using var tabs = await TwoSessionsAsync();
        _ = await tabs.Other.Page.GotoAsync(Route);
        var ofTheCurrent = await tabs.Other.Page.Locator($"{Rows} a").First.GetAttributeAsync("href");
        Assert.NotNull(ofTheCurrent);
        var page = tabs.Current.Page;

        _ = await page.GotoAsync($"{End}?sessao=ZQX-nao-existe");
        var absentPage = await page.ContentAsync();
        _ = await page.GotoAsync(ofTheCurrent);
        var currentPage = await page.ContentAsync();
        _ = await page.GotoAsync(Missing);
        var missingPage = await page.ContentAsync();

        Assert.Equal(StripAddress(missingPage, Missing), StripAddress(absentPage, End));
        Assert.Equal(StripAddress(missingPage, Missing), StripAddress(currentPage, End));
    }

    [Fact]
    public async Task Ca3AnAddressThatNamesNoSessionOfTheListEndsNothingAndTheOtherSessionStaysOpen()
    {
        await using var tabs = await TwoSessionsAsync();
        var page = tabs.Current.Page;

        _ = await page.GotoAsync($"{End}?sessao=ZQX-nao-existe");
        _ = await page.Context.APIRequest.PostAsync(new Uri(new Uri(page.Url), End).AbsoluteUri, new() { Form = page.Context.APIRequest.CreateFormData().Set("sessao", "ZQX-nao-existe") });

        _ = await page.GotoAsync(Route);
        await Expect(page.Locator(Rows)).ToHaveCountAsync(2);
        _ = await tabs.Other.Page.GotoAsync(Route);
        await Expect(tabs.Other.Page.Locator(Rows)).ToHaveCountAsync(2);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheAccountHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tabs = await TwoSessionsAsync();
        await mode.ApplyAsync(tabs.Current.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tabs.Current.Page);
        await RealBackgroundContrast.AssertAaAsync(tabs.Current.Page);
        await ModeCapture.SaveAsync(tabs.Current.Page, mode, "conta");
    }

    [Fact]
    public async Task TheAccountFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation()
    {
        await using var tabs = await TwoSessionsAsync();
        await tabs.Current.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tabs.Current.Page.GotoAsync(Route);

        var overflow = await tabs.Current.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tabs.Current.Page);
        await RealBackgroundContrast.AssertAaAsync(tabs.Current.Page);
        await ModeCapture.SaveAsync(tabs.Current.Page, "celular", "conta");
    }

    [Fact]
    public async Task OnThePhoneEachSessionIsABlockWithItsHeadersAndTheWayToEndItIsInSightWithoutScrollingTheTable()
    {
        await using var tabs = await TwoSessionsAsync();
        await tabs.Current.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tabs.Current.Page.GotoAsync(Route);

        var end = tabs.Current.Page.Locator($"{Rows} a");
        var box = await end.BoundingBoxAsync();
        var table = await tabs.Current.Page.Locator("#sessoes-tabela-legenda")
            .EvaluateAsync<int>("(caption) => { const region = caption.closest('.data-table'); return region.scrollWidth - region.clientWidth; }");
        var labels = await tabs.Current.Page.Locator($"{Rows} > *").EvaluateAllAsync<string[]>("(cells) => cells.map((cell) => getComputedStyle(cell, '::before').content)");

        await Expect(end).ToBeInViewportAsync(new() { Ratio = 1 });
        Assert.NotNull(box);
        Assert.InRange(box.X + box.Width, 0, PhoneWidth);
        Assert.True(table <= 0, $"The table scrolls {table} px sideways on the phone.");
        Assert.Equal(["\"Início\"", "\"Última atividade\"", "\"Ação\""], labels.Take(3));
        Assert.Equal("block", await tabs.Current.Page.Locator(Rows).First.EvaluateAsync<string>("(row) => getComputedStyle(row).display"));
    }

    [Fact]
    public async Task OnThePhoneTheStackedTableStaysATableForAReaderOfScreensWithItsRowsHeadersAndCells()
    {
        await using var tabs = await TwoSessionsAsync();
        await tabs.Current.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tabs.Current.Page.GotoAsync(Route);
        var cdp = await tabs.Current.Page.Context.NewCDPSessionAsync(tabs.Current.Page);
        _ = await cdp.SendAsync("Accessibility.enable");

        var tree = await cdp.SendAsync("Accessibility.getFullAXTree");

        var roles = tree!.Value.GetProperty("nodes").EnumerateArray()
            .Where(node => !node.TryGetProperty("ignored", out var ignored) || !ignored.GetBoolean())
            .Select(node => node.TryGetProperty("role", out var role) ? role.GetProperty("value").GetString() : null)
            .ToList();
        Assert.Equal((1, 3, 3, 2, 4), (roles.Count(role => role == "table"), roles.Count(role => role == "row"), roles.Count(role => role == "columnheader"),
            roles.Count(role => role == "rowheader"), roles.Count(role => role == "cell")));
    }

    [Fact]
    public async Task OnAWideScreenTheSessionsStayATableWithTheHeadersAboveTheColumns()
    {
        await using var tabs = await TwoSessionsAsync();

        var display = await tabs.Current.Page.Locator(Rows).First.EvaluateAsync<string>("(row) => getComputedStyle(row).display");

        Assert.Equal("table-row", display);
        await Expect(tabs.Current.Page.Locator("[aria-labelledby=sessoes-titulo] thead th").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AnAddressThatSaysAnActWasDoneShowsNoNoticeWhateverItsCount()
    {
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());

        _ = await tab.Page.GotoAsync($"{Route}?feito=senha&encerradas=-3");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        _ = await tab.Page.GotoAsync($"{Route}?feito=sessoes&encerradas=4");
        await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheNoticeOfAnActIsToldOnceAndNotAgainWhenTheAccountIsOpenedAgain()
    {
        await using var tabs = await TwoSessionsAsync();
        await tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Encerrar todas as outras" }).ClickAsync();
        await tabs.Current.Page.Locator("#encerrar-outras").ClickAsync();
        await Expect(tabs.Current.Page.Locator(".notice--success")).ToContainTextAsync("A sessão foi encerrada.");

        _ = await tabs.Current.Page.ReloadAsync();

        await Expect(tabs.Current.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
        await Expect(tabs.Current.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        Assert.DoesNotContain(await tabs.Current.Page.Context.CookiesAsync(), cookie => cookie.Name == "__Host-feito");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheHeaderTheLinkOfTheAccountThePasswordAndEachWayToEndASessionInOrderWithTheFocusSeen()
    {
        await using var tabs = await TwoSessionsAsync();
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < HeaderStops.Count + PageStops; stop++)
        {
            await tabs.Current.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tabs.Current.Page.EvaluateAsync<string>(Focused));
            seen.Add(await tabs.Current.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        var header = reached.Take(HeaderStops.Count).Select(stop => stop.Split('|')).Select(parts => parts[0].Length > 0 ? parts[0] : parts[1]);
        Assert.Equal(HeaderStops.Signed, header);
        var page = reached.Skip(HeaderStops.Count).Select(stop => stop.Split('|')[2]);
        Assert.Equal(Ways, page.Select(text => Array.Find(Ways, way => text.StartsWith(way, StringComparison.Ordinal))).OfType<string>().ToList());
        Assert.DoesNotContain(false, seen);
    }

    [Fact]
    public async Task TheLinkOfTheAccountInTheHeaderMarksThePageItOpensAndLeadsToItFromTheStart()
    {
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());
        var link = tab.Page.GetByRole(AriaRole.Link, new() { Name = "Minha conta" });
        await Expect(link).ToHaveAttributeAsync("aria-current", "page");

        _ = await tab.Page.GotoAsync("/");
        await Expect(link).Not.ToHaveAttributeAsync("aria-current", "page");
        await link.ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex("/minha-conta$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
    }

    private static string StripAddress(string page, string route) => page.Replace(route, string.Empty, StringComparison.Ordinal);

    private async Task<Sessions> TwoSessionsAsync()
    {
        var person = await account.Host.AddPersonAsync();
        var other = await ScreenSession.SignedInAsync(browser, account.Host, person);
        try
        {
            return new Sessions(await OpenAsync(person), other);
        }
        catch
        {
            await other.DisposeAsync();
            throw;
        }
    }

    private async Task<BrowserTab> OpenAsync(string person)
    {
        var tab = await ScreenSession.SignedInAsync(browser, account.Host, person);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }

    private sealed record Sessions(BrowserTab Current, BrowserTab Other) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Current.DisposeAsync();
            await Other.DisposeAsync();
        }
    }
}
