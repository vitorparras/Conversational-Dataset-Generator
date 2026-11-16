using System.Text;
using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The submission of material and the batch it opens (issue 821) in a real Chromium, over the host with the real gates, the real routes and the real service: the form is filled and sent with
/// the keyboard alone, from the domain to the button, and the sending leads to the page of its batch; the form and the batch have no violation of WCAG 2.1 A and AA in the light and in the dark,
/// and fit the width of a phone and of 1280 px without scrolling sideways.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class SubmissionScreenTests(BrowserFixture browser, SubmissionHostFixture host) : IClassFixture<SubmissionHostFixture>
{
    private const string Route = "/lotes/novo";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";

    [Fact]
    public async Task SubmissionReachesTheBatchByKeyboard()
    {
        await using var tab = await OpenAsync();

        var batch = await SubmitByKeyboardAsync(tab.Page);

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Lote {batch}", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("main")).Not.ToContainTextAsync("ZQXPRIMEIRO");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task SubmissionScreenPassesTheAccessibilityCheck(string name)
    {
        await using var tab = await OpenAsync();

        await AssertAccessibleAsync(tab.Page, MediaMode.Named(name), "envio");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task BatchScreenPassesTheAccessibilityCheck(string name)
    {
        await using var tab = await OpenAsync();
        var batch = await SubmitByKeyboardAsync(tab.Page);
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Lote {batch}", Level = 1 })).ToBeVisibleAsync();

        await AssertAccessibleAsync(tab.Page, MediaMode.Named(name), "lote");
    }

    /// <summary>The checks of the screen in the mode given, on the desktop and on the phone, which must not scroll sideways.</summary>
    private static async Task AssertAccessibleAsync(IPage page, MediaMode mode, string capture)
    {
        await mode.ApplyAsync(page);
        await page.SetViewportSizeAsync(1280, 800);
        await AccessibilityCheck.AssertNoViolationsAsync(page);
        await RealBackgroundContrast.AssertAaAsync(page);
        await ModeCapture.SaveAsync(page, mode, capture);

        await page.SetViewportSizeAsync(390, 844);
        var overflow = await page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than 390 px.");
        await AccessibilityCheck.AssertNoViolationsAsync(page);
        await RealBackgroundContrast.AssertAaAsync(page);
    }

    /// <summary>
    /// Fills the form with the keyboard, in the order of its fields, and sends it with Enter; the file alone is chosen by its label, as the dialog of the system would. Answers the number of the
    /// batch the sending led to.
    /// </summary>
    private static async Task<string> SubmitByKeyboardAsync(IPage page)
    {
        var domain = page.GetByRole(AriaRole.Radio, new() { Name = SubmissionCase.Granted });
        await domain.FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Expect(domain).ToBeCheckedAsync();

        var file = page.GetByLabel("Arquivo");
        await file.SetInputFilesAsync(new FilePayload { Name = "conversa.json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(SubmissionCase.Conversation) });
        await file.FocusAsync();

        await page.Keyboard.PressAsync("Tab");
        var form = page.GetByRole(AriaRole.Radio, new() { Name = "Conversa: mensagens, cada uma com autor e instante" });
        await Expect(form).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Space");
        await Expect(form).ToBeCheckedAsync();

        await page.Keyboard.PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Radio, new() { Name = SubmissionCase.Format })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Radio, new() { Name = "zqx_sigilosa (a mais restrita)" })).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Tab");
        await Expect(page.GetByLabel("Origem do material")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("zqx-origem-do-teste");
        await page.Keyboard.PressAsync("Tab");
        await Expect(page.GetByLabel("A origem é desconhecida")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Enviar material" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        await Expect(page).ToHaveURLAsync(BatchAddress());
        return BatchAddress().Match(page.Url).Groups["id"].Value;
    }

    [GeneratedRegex(@"/lotes/(?<id>\d+)$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex BatchAddress();

    private async Task<BrowserTab> OpenAsync()
    {
        var tab = await ScreenSession.SignedInAsync(browser, host.Host, host.Host.Curator);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Enviar material", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
