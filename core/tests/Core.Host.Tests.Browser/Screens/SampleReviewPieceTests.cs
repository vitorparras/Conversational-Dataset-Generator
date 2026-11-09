using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The pieces of the review by sample (issue 610) in a real Chromium, at the addresses the host of the test gives them: no item is skipped, since only the next one offers the mark;
/// the approval is absent until every item is reviewed; the keyboard alone goes through the whole sample, reaches the rejection of an item and comes back, and offers the approval only at
/// the end; the text of an item is beside the item and nowhere else, and never in the rejection; and the pages have no violation of WCAG 2.1 A and AA in the light and in the dark and fit the
/// width of a phone and of 1280 px.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SampleReviewPieceTests(BrowserFixture browser, SampleReviewHostFixture review) : IClassFixture<SampleReviewHostFixture>
{
    private const string Review = SampleReviewPieceHost.Review;
    private const string Reject = SampleReviewPieceHost.Reject;
    private const int Items = SampleReviewPieceHost.Items;
    private const int MostStops = 20;
    private const string Mark = "Revisado, sem dado pessoal";
    private const string Finding = "Achei dado pessoal";
    private const string ReviewTitle = "Revisão da amostra do lote 41";
    private const string RejectTitle = "Reprovar o lote 41";
    private const string Material = "ZQX-texto-do-item";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusTheTitle = "() => { const title = document.querySelector('main h1'); title.setAttribute('tabindex', '-1'); title.focus(); }";
    private const string StopInMain = "() => { const stop = document.activeElement; return stop && stop.closest('main') ? (stop.id || stop.textContent.trim()) : ''; }";
    private const string WithoutTheItems =
        "() => { const main = document.querySelector('main').cloneNode(true); " +
        "main.querySelectorAll('.sample__text').forEach(text => text.remove()); return main.innerText; }";

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    [InlineData(4, 5)]
    public async Task OnlyTheNextItemOffersTheMarkAndNoItemIsSkipped(int reviewed, int next)
    {
        await using var tab = await OpenAsync($"{Review}?e=revisados-{reviewed}", ReviewTitle);

        await Expect(tab.Page.GetByRole(AriaRole.Button, new() { Name = Mark })).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator($"#revisado-{next}")).ToBeVisibleAsync();
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = Finding })).ToHaveCountAsync(Items);
        await Expect(tab.Page.Locator(".mark--good")).ToHaveCountAsync(reviewed);
        await Expect(tab.Page.GetByText("Revise os itens anteriores primeiro.")).ToHaveCountAsync(Items - reviewed - 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task TheApprovalIsAbsentUntilEveryItemIsReviewed(int reviewed)
    {
        await using var tab = await OpenAsync($"{Review}?e=revisados-{reviewed}", ReviewTitle);

        await Expect(tab.Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Aprovar o lote") })).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator("#aprovar")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Aprovar ainda não é possível.");
    }

    [Fact]
    public async Task WithEveryItemReviewedTheApprovalAppearsNoMarkIsLeftAndAFindingCanStillBeTold()
    {
        await using var tab = await OpenAsync($"{Review}?e=revisados-{Items}", ReviewTitle);

        await Expect(tab.Page.GetByRole(AriaRole.Button, new() { Name = "Aprovar o lote 41" })).ToBeVisibleAsync();
        await Expect(tab.Page.GetByRole(AriaRole.Button, new() { Name = Mark })).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = Finding })).ToHaveCountAsync(Items);
        await Expect(tab.Page.Locator(".mark--good")).ToHaveCountAsync(Items);
    }

    [Theory]
    [InlineData("aprovada", ".notice--success", "Lote aprovado por zqx-curadoria", "O lote já foi aprovado.")]
    [InlineData("reprovada", ".notice--info", "Lote reprovado por zqx-curadoria", "O lote já foi reprovado.")]
    public async Task ADecidedBatchSaysWhoDecidedAndOffersNothingToDo(string state, string notice, string who, string conclusion)
    {
        await using var tab = await OpenAsync($"{Review}?e={state}", ReviewTitle);

        await Expect(tab.Page.Locator(notice)).ToContainTextAsync(who);
        await Expect(tab.Page.GetByText(conclusion)).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("#aprovar")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator("main form")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = Finding })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ARejectedBatchSaysTheKindFoundAndTheItemNeverTheData()
    {
        await using var tab = await OpenAsync($"{Review}?e=reprovada", ReviewTitle);

        await Expect(tab.Page.Locator(".notice--info")).ToContainTextAsync("Contato: telefone, e-mail ou perfil no item 3 da amostra");
        await Expect(tab.Page.GetByText("Não revisado: o lote já foi decidido.")).ToHaveCountAsync(Items - 2);
    }

    [Fact]
    public async Task TheKeyboardAloneGoesThroughTheWholeSampleAndOffersTheApprovalOnlyAtTheEnd()
    {
        await using var tab = await OpenAsync($"{Review}?e=revisados-0", ReviewTitle);

        for (var position = 1; position <= Items; position++)
        {
            Assert.Equal(StopsAt(position), await StopsInMainAsync(tab.Page));

            await PressTabAsync(tab.Page, position);
            await tab.Page.Keyboard.PressAsync("Enter");

            await Expect(tab.Page).ToHaveURLAsync(new Regex($"e=revisados-{position}$"));
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = ReviewTitle, Level = 1 })).ToBeVisibleAsync();
        }

        Assert.Equal([.. FindingsBetween(1, Items), "aprovar"], await StopsInMainAsync(tab.Page));
        await PressTabAsync(tab.Page, Items + 1);
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("e=aprovada$"));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Lote aprovado por");
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(1, 1)]
    public async Task TheKeyboardAloneReachesTheRejectionOfAnItemReviewedOrNotAndComesBackWithoutRejecting(int tabs, int item)
    {
        await using var tab = await OpenAsync($"{Review}?e=revisados-2", ReviewTitle);

        await PressTabAsync(tab.Page, tabs);
        Assert.Equal($"achado-{item}", await tab.Page.EvaluateAsync<string>(StopInMain));
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = RejectTitle, Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".notice")).ToContainTextAsync($"item {item} da amostra");
        var kinds = await tab.Page.Locator("input[type=radio]").EvaluateAllAsync<string[]>("inputs => inputs.map(input => input.value)");
        Assert.Equal(["contato", "documento", "endereco", "nome", "outro", "sensivel"], kinds.Order(StringComparer.Ordinal));
        var stops = await StopsInMainAsync(tab.Page);
        Assert.Equal("reprovar-lote", stops[^2]);
        Assert.Equal("Voltar à amostra sem reprovar", stops[^1]);

        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar à amostra sem reprovar" }).FocusAsync();
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"e=revisados-{item - 1}$"));
        await Expect(tab.Page.Locator($"#revisado-{item}")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TheTextOfTheItemsIsBesideTheItemsAndNowhereElse()
    {
        await using var tab = await OpenAsync($"{Review}?e=revisados-2", ReviewTitle);

        await Expect(tab.Page.Locator(".sample__text")).ToHaveCountAsync(Items);
        Assert.Equal(Enumerable.Range(1, Items).Select(SampleReviewPieceHost.TextOf), await tab.Page.Locator(".sample__text").AllInnerTextsAsync());
        Assert.DoesNotContain(Material, await tab.Page.EvaluateAsync<string>(WithoutTheItems), StringComparison.Ordinal);
        Assert.DoesNotContain(Material, await tab.Page.TitleAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(Material, tab.Page.Url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Reject)]
    [InlineData(Reject + "?e=tipo")]
    public async Task TheRejectionHasNoTextOfTheMaterialAndNoFieldToWriteOne(string route)
    {
        await using var tab = await OpenAsync(route, RejectTitle);

        Assert.DoesNotContain(Material, await tab.Page.ContentAsync(), StringComparison.Ordinal);
        await Expect(tab.Page.Locator("main textarea, main input[type=text], main blockquote")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Button, new() { Name = "Reprovar o lote inteiro" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TheKindChosenIsGivenBackChecked()
    {
        await using var tab = await OpenAsync($"{Reject}?e=tipo", RejectTitle);

        await Expect(tab.Page.GetByLabel("Contato: telefone, e-mail ou perfil")).ToBeCheckedAsync();
        await Expect(tab.Page.GetByLabel("Nome de pessoa")).Not.ToBeCheckedAsync();
    }

    [Theory]
    [InlineData("Light", "revisados-0")]
    [InlineData("Dark", "revisados-0")]
    [InlineData("Light", "revisados-5")]
    [InlineData("Dark", "revisados-5")]
    [InlineData("Light", "aprovada")]
    [InlineData("Dark", "aprovada")]
    [InlineData("Light", "reprovada")]
    [InlineData("Dark", "reprovada")]
    public async Task TheReviewHasNoViolationOfWcagAAndAaInTheLightAndInTheDark(string name, string state)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync($"{Review}?e={state}", ReviewTitle);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, $"revisao-{state}");
    }

    [Theory]
    [InlineData("Light", Reject, "reprovar")]
    [InlineData("Dark", Reject, "reprovar")]
    [InlineData("Light", Reject + "?e=tipo", "reprovar-tipo")]
    [InlineData("Dark", Reject + "?e=tipo", "reprovar-tipo")]
    public async Task TheRejectionHasNoViolationOfWcagAAndAaInTheLightAndInTheDark(string name, string route, string capture)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(route, RejectTitle);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, capture);
    }

    [Theory]
    [InlineData(Review + "?e=revisados-0", ReviewTitle, 390, 844)]
    [InlineData(Review + "?e=revisados-5", ReviewTitle, 390, 844)]
    [InlineData(Review + "?e=revisados-0", ReviewTitle, 1280, 800)]
    [InlineData(Review + "?e=revisados-5", ReviewTitle, 1280, 800)]
    [InlineData(Reject, RejectTitle, 390, 844)]
    [InlineData(Reject, RejectTitle, 1280, 800)]
    public async Task ThePagesFitThePhoneAndTheDesktopWithoutScrollingSidewaysAndHaveNoViolation(string route, string title, int width, int height)
    {
        await using var tab = await OpenAsync(route, title);
        await tab.Page.SetViewportSizeAsync(width, height);
        _ = await tab.Page.GotoAsync(route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"{route}: the page is {overflow} px wider than {width} px.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    /// <summary>The stops of the keyboard inside the content, from the title on, until the focus leaves it: the id of the stop or, without one, its text.</summary>
    private static async Task<List<string>> StopsInMainAsync(IPage page)
    {
        await FocusTheTitleAsync(page);
        var stops = new List<string>();
        for (var press = 0; press < MostStops; press++)
        {
            await page.Keyboard.PressAsync("Tab");
            var stop = await page.EvaluateAsync<string>(StopInMain);
            if (stop.Length == 0)
            {
                break;
            }

            stops.Add(stop);
        }

        return stops;
    }

    private static async Task FocusTheTitleAsync(IPage page) => _ = await page.EvaluateAsync(FocusTheTitle);

    /// <summary>From the title, the keyboard goes forward a number of stops.</summary>
    private static async Task PressTabAsync(IPage page, int stops)
    {
        await FocusTheTitleAsync(page);
        for (var press = 0; press < stops; press++)
        {
            await page.Keyboard.PressAsync("Tab");
        }
    }

    /// <summary>The finding of every item from the first to the last given, which stays on until the batch is decided.</summary>
    private static IEnumerable<string> FindingsBetween(int first, int last) => Enumerable.Range(first, last - first + 1).Select(position => $"achado-{position}");

    /// <summary>The stops of the review with the item at the position as the next: the findings before it, its mark, and the findings from it to the end.</summary>
    private static List<string> StopsAt(int position) => [.. FindingsBetween(1, position - 1), $"revisado-{position}", .. FindingsBetween(position, Items)];

    private async Task<BrowserTab> OpenAsync(string route, string title)
    {
        var tab = await ScreenSession.SignedInAsync(browser, review.Host, review.Host.Curator);
        try
        {
            _ = await tab.Page.GotoAsync(route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = title, Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
