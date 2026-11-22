using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screens of the rerun of the sanitization (issue 97) in a real Chromium, over the host with the real gates, the real routes and the desk the root registers: the rerun is confirmed on a page of
/// its own and sent by the keyboard alone, and the progress, with the items refused and their reasons, is walked by the keyboard; every page has no violation of WCAG 2.1 A and AA, in the light and in
/// the dark, on 1280 px and on the width of a phone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class RerunScreenTests(BrowserFixture browser, RerunHostFixture rerun) : IClassFixture<RerunHostFixture>
{
    private const string Situation = "/sanitizacao";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";

    [Fact]
    public async Task RerunIsConfirmedByKeyboardAndPassesTheAccessibilityCheck()
    {
        await using var tab = await EmptyingPages.OpenAsync(browser, rerun.Host, rerun.Host.Curator, Situation);
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reexecucao-situacao");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reexecucao-situacao");
        await tab.Page.SetViewportSizeAsync(1280, 800);

        await FocusAsync(tab.Page, "document.activeElement.id === 'reexecutar'");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Confirmar a reexecução", Level = 1 })).ToBeVisibleAsync();
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reexecucao-confirmacao");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reexecucao-confirmacao");
        await tab.Page.SetViewportSizeAsync(1280, 800);
        await FocusAsync(tab.Page, "document.activeElement.id === 'reexecutar'");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("/sanitizacao/reexecucoes/[0-9]+$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator(".facts__item", new() { HasText = "Situação" }).Locator("dd")).ToHaveTextAsync("Na fila");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reexecucao-na-fila");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reexecucao-na-fila");
    }

    [Fact]
    public async Task ProgressIsReadByKeyboardAndPassesTheAccessibilityCheck()
    {
        var done = await rerun.Case.RefusedEveryItemAsync();
        await using var tab = await EmptyingPages.OpenAsync(browser, rerun.Host, rerun.Host.Curator, $"/sanitizacao/reexecucoes/{done}");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reexecucao-concluida");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reexecucao-concluida");
        await tab.Page.SetViewportSizeAsync(1280, 800);

        var refusals = tab.Page.GetByRole(AriaRole.Region, new() { Name = "Cada linha é um item recusado, com o motivo" });
        await Expect(refusals.GetByRole(AriaRole.Cell, new() { Name = "Falhou em todas as tentativas." })).ToHaveCountAsync(RerunCase.Items);
        await Expect(tab.Page.Locator(".facts__item", new() { HasText = "Recusados" }).Locator("dd")).ToHaveTextAsync("3");

        await FocusAsync(tab.Page, "document.activeElement.getAttribute('aria-labelledby') === 'recusados-legenda'");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await FocusAsync(tab.Page, "document.activeElement.textContent.trim() === 'Voltar para a situação'");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("/sanitizacao$", RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    /// <summary>Presses Tab until the condition holds of the element with the focus, past the stops of the header; fails when it never does.</summary>
    private static async Task FocusAsync(IPage page, string condition)
    {
        for (var stop = 0; stop < HeaderStops.Count + 12; stop++)
        {
            await page.Keyboard.PressAsync("Tab");
            if (await page.EvaluateAsync<bool>($"() => {condition}"))
            {
                return;
            }
        }

        Assert.Fail($"The keyboard never reached what holds: {condition}.");
    }
}
