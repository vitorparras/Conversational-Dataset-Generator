using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PhaseThreeScreens;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The finish of the screens of phase 3 (issue 734, part A) in a real Chromium, over the host that holds every screen the main has: the header takes at most 15% of the height of the phone on
/// every screen with a header; going through the whole page with the keyboard, forward and back, no focus is hidden under the header that stays on top; no table of the phase cuts a
/// column or scrolls sideways at 390 px; and the notices have one word, "não lidos", in the header, on the start page and in the list.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class PhaseThreeFinishTests(BrowserFixture browser, PhaseThreeHostFixture phase) : IClassFixture<PhaseThreeHostFixture>
{
    private const int MostStops = 80;
    private const string Unread = "não lido";

    public static TheoryData<string, bool> HeaderCases { get; } = Cases();

    public static TheoryData<string, bool, int, int> FocusCases { get; } = FocusCasesOf();

    public static TheoryData<string> TableCases { get; } = [.. WithTable];

    [Theory]
    [MemberData(nameof(HeaderCases))]
    public async Task TheHeaderTakesAtMostAFifthOfThePhoneHeightOnEveryScreen(string route, bool noticed)
    {
        await using var tab = await OpenAsync(route, noticed, PhoneWidth, PhoneHeight);

        var height = await tab.Page.EvaluateAsync<double>(HeaderHeight);

        Assert.True(height <= PhoneHeight * HeaderShareOfPhone, $"{route}: the header is {height:0} px of {PhoneHeight}, which is {height / PhoneHeight:P0}; the most is {HeaderShareOfPhone:P0}.");
    }

    [Fact]
    public async Task TheHeaderOfTheEntryWithoutSessionTakesAtMostAFifthOfThePhoneHeight()
    {
        await using var tab = await browser.OpenAtAsync(phase.Host.Address, "/entrada");
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        var height = await tab.Page.EvaluateAsync<double>(HeaderHeight);

        Assert.True(height <= PhoneHeight * HeaderShareOfPhone, $"/entrada: the header is {height:0} px of {PhoneHeight}.");
    }

    [Theory]
    [MemberData(nameof(FocusCases))]
    public async Task NoFocusIsHiddenUnderTheHeaderGoingThroughThePageForwardAndBack(string route, bool noticed, int width, int height)
    {
        await using var tab = await OpenAsync(route, noticed, width, height);
        var hidden = new List<string>();

        hidden.AddRange(await GoThroughAsync(tab.Page, "Tab"));
        _ = await tab.Page.ReloadAsync();
        hidden.AddRange(await GoThroughAsync(tab.Page, "Shift+Tab"));

        Assert.True(hidden.Count == 0, $"{route} at {width}x{height}: {string.Join("; ", hidden)}.");
    }

    [Theory]
    [MemberData(nameof(TableCases))]
    public async Task NoTableOfThePhaseCutsAColumnOrScrollsSidewaysAtPhoneWidth(string route)
    {
        await using var tab = await OpenAsync(route, route == Notices, PhoneWidth, PhoneHeight);

        Assert.True(await tab.Page.Locator("main table").CountAsync() > 0, $"{route}: the page has no table, so the check would prove nothing.");
        var cut = await tab.Page.EvaluateAsync<string[]>(CutTables);

        Assert.True(cut.Length == 0, $"{route}: {string.Join("; ", cut)}.");
    }

    [Fact]
    public async Task TheNoticesHaveOneWordInTheHeaderOnTheStartPageAndInTheList()
    {
        await using var tab = await OpenAsync("/", noticed: true, DesktopWidth, DesktopHeight);

        var header = await tab.Page.Locator(".header__notices").InnerTextAsync();
        var start = await tab.Page.Locator("main .notice").First.InnerTextAsync();
        _ = await tab.Page.GotoAsync(Notices);
        var list = await tab.Page.Locator("main").InnerTextAsync();

        foreach (var (place, text) in new[] { ("header", header), ("start page", start), ("list", list) })
        {
            Assert.True(text.Contains(Unread, StringComparison.OrdinalIgnoreCase), $"The {place} does not say \"{Unread}\": {text}");
            Assert.False(NewNotices().IsMatch(text), $"The {place} says \"new\" for the notices that were not read: {text}");
        }
    }

    [Fact]
    public async Task TheListWithNothingToReadDoesNotCallTheNoticesNew()
    {
        await using var tab = await OpenAsync(Notices, noticed: false, DesktopWidth, DesktopHeight);

        await Expect(tab.Page.Locator("main .empty-state")).ToBeVisibleAsync();
        var text = await tab.Page.Locator("main").InnerTextAsync();

        Assert.False(NewNotices().IsMatch(text), $"The empty list says \"new\" for the notices that were not read: {text}");
    }

    [GeneratedRegex(@"\bavisos? novos?\b|\bnovos? avisos?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NewNotices();

    private static TheoryData<string, bool> Cases()
    {
        var cases = new TheoryData<string, bool>();
        foreach (var route in WithHeader)
        {
            cases.Add(route, false);
        }

        cases.Add("/", true);
        return cases;
    }

    private static TheoryData<string, bool, int, int> FocusCasesOf()
    {
        var cases = new TheoryData<string, bool, int, int>();
        foreach (var route in WithHeader)
        {
            cases.Add(route, false, PhoneWidth, PhoneHeight);
            cases.Add(route, false, DesktopWidth, DesktopHeight);
        }

        cases.Add("/", true, PhoneWidth, PhoneHeight);
        return cases;
    }

    /// <summary>
    /// The focus goes through the page one stop at a time, in the direction given, until it leaves the page; each stop outside the header whose top is above the bottom of the header is
    /// hidden.
    /// </summary>
    private static async Task<List<string>> GoThroughAsync(IPage page, string key)
    {
        var hidden = new List<string>();
        var stop = 0;
        double[]? at = null;
        do
        {
            await page.Keyboard.PressAsync(key);
            at = await page.EvaluateAsync<double[]?>(FocusAgainstHeader);
            if (at is { Length: 4 } && at[3] == 0 && at[0] < at[2] - 0.5)
            {
                hidden.Add($"{key}: \"{await page.EvaluateAsync<string>(FocusedName)}\" starts at {at[0]:0} px and the header goes down to {at[2]:0} px");
            }

            stop++;
        }
        while (at is not null && stop < MostStops);

        return hidden;
    }

    private async Task<BrowserTab> OpenAsync(string route, bool noticed, int width, int height)
    {
        var tab = await ScreenSession.SignedInAsync(browser, phase.Host, noticed ? phase.Host.Noticed : phase.Host.Administrator);
        try
        {
            await tab.Page.SetViewportSizeAsync(width, height);
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
