using System.Globalization;
using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The review by sample (issue 822) in a real Chromium, over the host with the real gates, the real routes, the real desk and a batch opened by the real service of the submissions: the draw,
/// each item, the finding and the approval are reached with the keyboard alone and write what the installation writes; the three screens (the draw, the review and the rejection) have no
/// violation of WCAG 2.1 A and AA in the light and in the dark and fit the width of a phone and of 1280 px; the text of the items is beside the items and nowhere else.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SampleReviewBrowserTests(BrowserFixture browser, SampleReviewRealHostFixture real) : IClassFixture<SampleReviewRealHostFixture>
{
    private const int Items = SampleReviewRealCase.Items;
    private const int MostStops = 20;
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusTheTitle = "() => { const title = document.querySelector('main h1'); title.setAttribute('tabindex', '-1'); title.focus(); }";
    private const string StopInMain =
        "() => { const stop = document.activeElement; if (!stop || !stop.closest('main')) { return ''; } " +
        "const label = stop.labels && stop.labels.length > 0 ? stop.labels[0].textContent.trim() : ''; " +
        "return stop.id || stop.textContent.trim() || label; }";
    private const string WithoutTheItems =
        "() => { const main = document.querySelector('main').cloneNode(true); " +
        "main.querySelectorAll('.sample__text').forEach(text => text.remove()); return main.innerText; }";

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task SampleDrawScreenPassesTheAccessibilityCheck(string name)
    {
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);

        await Expect(tab.Page.Locator("#sortear")).ToBeVisibleAsync();
        await AssertAccessibleAsync(tab.Page, MediaMode.Named(name), "amostra-sorteio");
    }

    [Fact]
    public async Task SampleDrawIsReachedByKeyboardAndLeadsToTheFirstItem()
    {
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);

        Assert.Equal(["sortear", "Voltar para os lotes"], await StopsInMainAsync(tab.Page));
        await PressTabAsync(tab.Page, 1);
        await Expect(tab.Page.Locator("#sortear")).ToBeFocusedAsync();
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("resultado=sorteada$"));
        await Expect(tab.Page.Locator("#revisado-1")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".sample__text")).ToHaveCountAsync(Items);
    }

    [Fact]
    public async Task SampleReviewReachesEachItemByKeyboard()
    {
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);
        await DrawByKeyboardAsync(tab.Page);

        for (var position = 1; position <= Items; position++)
        {
            Assert.Equal(StopsAt(position), await StopsInMainAsync(tab.Page));

            await PressTabAsync(tab.Page, position);
            Assert.Equal($"revisado-{position}", await tab.Page.EvaluateAsync<string>(StopInMain));
            await tab.Page.Keyboard.PressAsync("Enter");

            await Expect(tab.Page).ToHaveURLAsync(new Regex("resultado=revisado$"));
            await Expect(tab.Page.Locator(".mark--good")).ToHaveCountAsync(position);
        }

        Assert.Equal([.. FindingsBetween(1, Items), "aprovar"], await StopsInMainAsync(tab.Page));
        await PressTabAsync(tab.Page, Items + 1);
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("resultado=aprovado$"));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Lote aprovado por");
        await Expect(tab.Page.Locator("#aprovar")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheRejectionOfAnItemAndRejectsTheBatch()
    {
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);
        await DrawByKeyboardAsync(tab.Page);

        await PressTabAsync(tab.Page, 2);
        Assert.Equal("achado-1", await tab.Page.EvaluateAsync<string>(StopInMain));
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Reprovar o lote {batch}", Level = 1 })).ToBeVisibleAsync();

        await tab.Page.GetByLabel("Nome de pessoa").CheckAsync();
        await tab.Page.Locator("#reprovar-lote").FocusAsync();
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("resultado=reprovado$"));
        await Expect(tab.Page.Locator(".notice--info")).ToContainTextAsync("Lote reprovado por");
        await Expect(tab.Page.Locator("main form")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task SampleReviewScreenPassesTheAccessibilityCheck(string name)
    {
        var mode = MediaMode.Named(name);
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);
        await DrawAsync(tab.Page);
        await AssertAccessibleAsync(tab.Page, mode, "revisao-sorteada");

        await MarkAsync(tab.Page, 1);
        await AssertAccessibleAsync(tab.Page, mode, "revisao-parcial");

        await MarkAsync(tab.Page, 2);
        await MarkAsync(tab.Page, 3);
        await Expect(tab.Page.Locator("#aprovar")).ToBeVisibleAsync();
        await AssertAccessibleAsync(tab.Page, mode, "revisao-completa");

        await tab.Page.Locator("#aprovar").ClickAsync();
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Lote aprovado por");
        await AssertAccessibleAsync(tab.Page, mode, "revisao-aprovada");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task RejectionScreenPassesTheAccessibilityCheck(string name)
    {
        var mode = MediaMode.Named(name);
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);
        await DrawAsync(tab.Page);
        await tab.Page.Locator("#achado-1").ClickAsync();
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Reprovar o lote {batch}", Level = 1 })).ToBeVisibleAsync();
        await AssertAccessibleAsync(tab.Page, mode, "reprovar");

        await tab.Page.Locator("#reprovar-lote").ClickAsync();
        await Expect(tab.Page.GetByText("Escolha o que foi achado.").First).ToBeVisibleAsync();
        await AssertAccessibleAsync(tab.Page, mode, "reprovar-sem-tipo");
    }

    [Fact]
    public async Task TheTextOfTheItemsIsBesideTheItemsAndNowhereElse()
    {
        var tag = Tag();
        var batch = await NewBatchAsync(tag);
        await using var tab = await OpenAsync(batch);
        await DrawAsync(tab.Page);
        var texts = Enumerable.Range(1, Items).Select(position => SampleReviewRealCase.TextOf(tag, position)).Order(StringComparer.Ordinal);

        await Expect(tab.Page.Locator(".sample__text")).ToHaveCountAsync(Items);
        Assert.Equal(texts, (await tab.Page.Locator(".sample__text").AllInnerTextsAsync()).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("ZQXTEXTO", await tab.Page.EvaluateAsync<string>(WithoutTheItems), StringComparison.Ordinal);
        Assert.DoesNotContain("ZQXTEXTO", await tab.Page.TitleAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("ZQXTEXTO", tab.Page.Url, StringComparison.Ordinal);

        await tab.Page.Locator("#achado-1").ClickAsync();
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Reprovar o lote {batch}", Level = 1 })).ToBeVisibleAsync();

        Assert.DoesNotContain("ZQXTEXTO", await tab.Page.ContentAsync(), StringComparison.Ordinal);
        await Expect(tab.Page.Locator("main textarea, main input[type=text], main blockquote")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AnApprovedBatchNoLongerShowsTheTextOfTheItems()
    {
        var batch = await NewBatchAsync();
        await using var tab = await OpenAsync(batch);
        await DrawAsync(tab.Page);
        for (var position = 1; position <= Items; position++)
        {
            await MarkAsync(tab.Page, position);
        }

        await tab.Page.Locator("#aprovar").ClickAsync();

        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Lote aprovado por");
        await Expect(tab.Page.Locator(".sample__text")).ToHaveCountAsync(0);
        Assert.DoesNotContain("ZQXTEXTO", await tab.Page.ContentAsync(), StringComparison.Ordinal);
    }

    /// <summary>The checks of the screen in the mode given, on the desktop and on the phone, which must not scroll sideways.</summary>
    private static async Task AssertAccessibleAsync(IPage page, MediaMode mode, string capture)
    {
        await mode.ApplyAsync(page);
        await page.SetViewportSizeAsync(1280, 800);
        var wide = await page.EvaluateAsync<int>(Overflow);
        Assert.True(wide <= 0, $"The page is {wide} px wider than 1280 px.");
        await AccessibilityCheck.AssertNoViolationsAsync(page);
        await RealBackgroundContrast.AssertAaAsync(page);
        await ModeCapture.SaveAsync(page, mode, capture);

        await page.SetViewportSizeAsync(390, 844);
        var overflow = await page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than 390 px.");
        await AccessibilityCheck.AssertNoViolationsAsync(page);
        await RealBackgroundContrast.AssertAaAsync(page);
    }

    /// <summary>Draws the sample with the keyboard alone, from the title to the button, and waits for the first item.</summary>
    private static async Task DrawByKeyboardAsync(IPage page)
    {
        await PressTabAsync(page, 1);
        await page.Keyboard.PressAsync("Enter");
        await Expect(page.Locator("#revisado-1")).ToBeVisibleAsync();
    }

    private static async Task DrawAsync(IPage page)
    {
        await page.Locator("#sortear").ClickAsync();
        await Expect(page.Locator("#revisado-1")).ToBeVisibleAsync();
    }

    /// <summary>Marks the item at the position as reviewed and waits until the page shows it as such.</summary>
    private static async Task MarkAsync(IPage page, int position)
    {
        await page.Locator($"#revisado-{position.ToString(CultureInfo.InvariantCulture)}").ClickAsync();
        await Expect(page.Locator(".mark--good")).ToHaveCountAsync(position);
    }

    /// <summary>The stops of the keyboard inside the content, from the title on, until the focus leaves it: the id of the stop or, without one, its text.</summary>
    private static async Task<List<string>> StopsInMainAsync(IPage page)
    {
        _ = await page.EvaluateAsync(FocusTheTitle);
        var stops = new List<string>();
        var stop = "início";
        for (var press = 0; press < MostStops && stop.Length > 0; press++)
        {
            await page.Keyboard.PressAsync("Tab");
            stop = await page.EvaluateAsync<string>(StopInMain);
            stops.Add(stop);
        }

        return [.. stops.Where(reached => reached.Length > 0)];
    }

    /// <summary>From the title, the keyboard goes forward a number of stops.</summary>
    private static async Task PressTabAsync(IPage page, int stops)
    {
        _ = await page.EvaluateAsync(FocusTheTitle);
        for (var press = 0; press < stops; press++)
        {
            await page.Keyboard.PressAsync("Tab");
        }
    }

    /// <summary>The finding of every item from the first to the last given, which stays on until the batch is decided.</summary>
    private static IEnumerable<string> FindingsBetween(int first, int last) => Enumerable.Range(first, last - first + 1).Select(position => $"achado-{position}");

    /// <summary>The stops of the review with the item at the position as the next: the mark and the finding of each item before it, its mark, and what follows.</summary>
    private static List<string> StopsAt(int position) =>
        [.. Enumerable.Range(1, position - 1).Select(before => $"achado-{before}"), $"revisado-{position}", .. FindingsBetween(position, Items)];

    private static string Tag() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();

    private async Task<long> NewBatchAsync(string? tag = null) => await SampleReviewRealCase.OpenBatchAsync(real.Host, tag ?? Tag());

    private async Task<BrowserTab> OpenAsync(long batch)
    {
        var tab = await ScreenSession.SignedInAsync(browser, real.Host, real.Host.Curator);
        try
        {
            _ = await tab.Page.GotoAsync($"/lotes/{batch.ToString(CultureInfo.InvariantCulture)}/revisao");
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Revisão da amostra do lote {batch.ToString(CultureInfo.InvariantCulture)}", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
