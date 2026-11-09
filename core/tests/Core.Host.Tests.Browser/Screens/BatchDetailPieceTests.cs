using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The pieces of the detail of a batch (issue 609) in a real Chromium, at the address the host of the test gives them: the counts by form, by destination of the passages and by replica,
/// with the batch that recognized the resending and the loss declared; nothing of the material in the page, only counts and the words of the screen; the keyboard alone reaches the one
/// link to another batch and the way back to the list; and the page has no violation of WCAG 2.1 A and AA in the light and in the dark and fits the width of a phone and of 1280 px.
/// </summary>
[Trait("Category", "Browser")]
public sealed class BatchDetailPieceTests(BrowserFixture browser, BatchDetailHostFixture detail) : IClassFixture<BatchDetailHostFixture>
{
    private const string Detail = BatchDetailPieceHost.Detail;
    private const string Back = "Voltar para os lotes";
    private const int MostStops = 12;
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusTheTitle = "() => { const title = document.querySelector('main h1'); title.setAttribute('tabindex', '-1'); title.focus(); }";
    private const string StopInMain = "() => { const stop = document.activeElement; return stop && stop.closest('main') ? stop.textContent.trim() : ''; }";

    private static readonly string[] Panels = ["Resumo", "Itens por forma", "Trechos identificáveis por destino", "Conteúdo replicado"];

    [Fact]
    public async Task AnAcceptedBatchShowsItsCountsByFormByDestinationAndByReplica()
    {
        await using var tab = await OpenAsync("aceito", "Lote 41");

        Assert.Equal(Panels, await tab.Page.Locator("main h2").AllInnerTextsAsync());
        var main = tab.Page.Locator("main");
        await Expect(main).ToContainTextAsync(BatchDetailPieceHost.Domain);
        await Expect(main).ToContainTextAsync("Aceito");
        await Expect(main).ToContainTextAsync(BatchDetailPieceHost.Submitter);
        await Expect(main).ToContainTextAsync("UTC");
        await Expect(main).ToContainTextAsync("Declarada e guardada selada");
        await Expect(main).ToContainTextAsync("Não é reenvio de um lote anterior.");
        foreach (var line in new[] { "Conversa", "12 itens", "Comentário", "3 itens", "Removidos", "4 trechos", "Pseudonimizados", "19 trechos", "Preservados", "2 trechos" })
        {
            await Expect(main).ToContainTextAsync(line);
        }

        await Expect(main).ToContainTextAsync("Mensagens marcadas como réplica");
        await Expect(main).ToContainTextAsync("Primeiras ocorrências desse conteúdo");
    }

    [Fact]
    public async Task ARejectedBatchSaysTheMotiveInWords()
    {
        await using var tab = await OpenAsync("rejeitado", "Lote 39");

        await Expect(tab.Page.Locator("main")).ToContainTextAsync("Rejeitado");
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("Uma resposta vinha antes da mensagem que ela responde, ou as respostas formavam um ciclo.");
        await Expect(tab.Page.Locator("main")).Not.ToContainTextAsync("order_incoherent");
    }

    [Fact]
    public async Task ABatchStillOpenHasNoItemAndSaysSoWithZeroPassagesInEveryDestination()
    {
        await using var tab = await OpenAsync("em-curso", "Lote 40");

        await Expect(tab.Page.Locator("main")).ToContainTextAsync("Em curso");
        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum item neste lote.");
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("O lote ainda está em curso; os itens aparecem aqui quando forem gravados.");
        await Expect(tab.Page.GetByText("0 trechos", new() { Exact = true })).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task TheLossDeclaredIsANoticeWithItsOwnPanelAndSaysWhatWasLost()
    {
        await using var tab = await OpenAsync("perda", "Lote 38");

        Assert.Equal(["Resumo", "Perda declarada", .. Panels[1..]], await tab.Page.Locator("main h2").AllInnerTextsAsync());
        var notice = tab.Page.Locator(".notice--info");
        await Expect(notice).ToHaveCountAsync(1);
        await Expect(notice.Locator(".notice__kind")).ToHaveTextAsync("Aviso:");
        await Expect(notice).ToContainTextAsync("O material veio como uma conversa e foi acomodado como um comentário, em 12 itens.");
        await Expect(tab.Page.Locator("main li")).ToHaveCountAsync(3);
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("A autoria por mensagem");
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("A estrutura de respostas");
        await Expect(tab.Page.Locator(".notice--error, [role=alert]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheBatchThatRecognizedTheResendingNamesTheEarlierOneWithALinkToItsDetail()
    {
        await using var tab = await OpenAsync("reenvio", "Lote 42");

        var link = tab.Page.GetByRole(AriaRole.Link, new() { Name = "lote 33" });
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("Reconhecido como reenvio do material do lote 33.");
        await Expect(link).ToHaveAttributeAsync("href", $"{Detail}?e=anterior");
    }

    [Theory]
    [InlineData("aceito")]
    [InlineData("rejeitado")]
    [InlineData("em-curso")]
    [InlineData("perda")]
    [InlineData("reenvio")]
    public async Task NothingOfTheMaterialIsInThePageOnlyCountsAndTheWordsOfTheScreen(string state)
    {
        await using var tab = await OpenAsync(state, null);

        await Expect(tab.Page.Locator("main blockquote, main pre, main code, main textarea, main input, main form")).ToHaveCountAsync(0);
        var addresses = await tab.Page.Locator("main a").EvaluateAllAsync<string[]>("links => links.map(link => link.getAttribute('href'))");
        Assert.All(addresses, address => Assert.Matches("^/captura/(lotes|lote\\?e=anterior)$", address));
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheWayBackToTheList()
    {
        await using var tab = await OpenAsync("aceito", "Lote 41");

        Assert.Equal([Back], await StopsInMainAsync(tab.Page));
        await FocusTheTitleAsync(tab.Page);
        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Regex.Escape(BatchDetailPieceHost.List)}$"));
    }

    [Fact]
    public async Task TheKeyboardAloneFollowsTheLinkToTheEarlierBatch()
    {
        await using var tab = await OpenAsync("reenvio", "Lote 42");

        Assert.Equal(["lote 33", Back], await StopsInMainAsync(tab.Page));
        await FocusTheTitleAsync(tab.Page);
        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Lote 33", Exact = true, Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("main")).ToContainTextAsync("Não é reenvio de um lote anterior.");
    }

    [Theory]
    [InlineData("Light", "aceito")]
    [InlineData("Dark", "aceito")]
    [InlineData("Light", "rejeitado")]
    [InlineData("Dark", "rejeitado")]
    [InlineData("Light", "em-curso")]
    [InlineData("Dark", "em-curso")]
    [InlineData("Light", "perda")]
    [InlineData("Dark", "perda")]
    [InlineData("Light", "reenvio")]
    [InlineData("Dark", "reenvio")]
    public async Task TheDetailHasNoViolationOfWcagAAndAaInTheLightAndInTheDark(string name, string state)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(state, null);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, $"detalhe-do-lote-{state}");
    }

    [Theory]
    [InlineData("aceito", 390, 844)]
    [InlineData("perda", 390, 844)]
    [InlineData("em-curso", 390, 844)]
    [InlineData("aceito", 1280, 800)]
    [InlineData("perda", 1280, 800)]
    public async Task TheDetailFitsThePhoneAndTheDesktopWithoutScrollingSidewaysAndHasNoViolation(string state, int width, int height)
    {
        await using var tab = await OpenAsync(state, null);
        await tab.Page.SetViewportSizeAsync(width, height);
        _ = await tab.Page.GotoAsync($"{Detail}?e={state}");

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"{state}: the page is {overflow} px wider than {width} px.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    /// <summary>The stops of the keyboard inside the content, from the title on, until the focus leaves it: the text of each.</summary>
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

    private async Task<BrowserTab> OpenAsync(string state, string? title)
    {
        var tab = await ScreenSession.SignedInAsync(browser, detail.Host, detail.Host.Curator);
        try
        {
            _ = await tab.Page.GotoAsync($"{Detail}?e={state}");
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = title ?? "Lote", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
