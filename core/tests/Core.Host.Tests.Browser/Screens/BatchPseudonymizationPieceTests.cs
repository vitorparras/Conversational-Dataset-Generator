using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The section of the pseudonymization in the detail of a batch (issue 101) in a real Chromium: a table of numbers alone, the markers by kind and the items that hold one, which the keyboard
/// reaches as a region named by its caption before the actions of the page, with no violation of WCAG 2.1 A and AA in the light and in the dark.
/// </summary>
[Trait("Category", "Browser")]
public sealed class BatchPseudonymizationPieceTests(BrowserFixture browser, BatchDetailHostFixture detail) : IClassFixture<BatchDetailHostFixture>
{
    private const string Caption = "Marcadores por tipo, só números";
    private const int MostStops = 12;
    private const string FocusTheTitle = "() => { const title = document.querySelector('main h1'); title.setAttribute('tabindex', '-1'); title.focus(); }";
    private const string StopInMain =
        "() => { const stop = document.activeElement; if (!stop || !stop.closest('main')) { return ''; } const named = stop.getAttribute('aria-labelledby'); "
        + "return named ? 'region:' + document.getElementById(named).textContent.trim() : stop.textContent.trim(); }";

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task PseudonymizationSectionIsReadByKeyboardAndPassesTheAccessibilityCheck(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await ScreenSession.SignedInAsync(browser, detail.Host, detail.Host.Curator);
        _ = await tab.Page.GotoAsync($"{BatchDetailPieceCase.Detail}?e=pseudonimizado");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Lote 13", Level = 1 })).ToBeVisibleAsync();
        await mode.ApplyAsync(tab.Page);

        var table = tab.Page.GetByRole(AriaRole.Region, new() { Name = Caption });
        Assert.Equal(
            [["Pessoa", "12", "31"], ["Lugar", "4", "9"], ["Organização", "3", "7"], ["Autor de mensagem", "5", "40"]],
            await table.Locator("tbody tr").EvaluateAllAsync<string[][]>("rows => rows.map(row => [...row.children].map(cell => cell.textContent.trim()))"));
        Assert.Equal([$"region:{Caption}", "Revisão da amostra", "Voltar para os lotes"], await StopsInMainAsync(tab.Page));
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Pseudonimização", Level = 2 })).ToBeVisibleAsync();
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "detalhe-do-lote-pseudonimizado");
    }

    /// <summary>The stops of the keyboard inside the content, from the title on, until the focus leaves it: a region by its caption, any other stop by its text.</summary>
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
}
