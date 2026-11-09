using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the seal (issue 736) as a person sees and walks it: no violation of WCAG 2.1 A and AA in the light, in the dark, on the width of a phone and on 1280 px, with the seal issued and
/// with a seal refused, reading over the glass; and reached and sent by the keyboard alone, with the focus seen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SealScreenAccessibilityTests(BrowserFixture browser, ManualSealHostFixture manual) : IClassFixture<ManualSealHostFixture>
{
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int WideWidth = 1280;
    private const int WideHeight = 800;

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheScreenHasNoViolationInTheLightAndInTheDarkBeforeAndAfterIssuingAndAfterARefusal(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync();
        await mode.ApplyAsync(tab.Page);
        await CheckAsync(tab, mode.Slug, "selo");

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Emitir selo" }).ClickAsync();
        await Expect(tab.Page.Locator("#selo-texto")).ToBeVisibleAsync();
        await CheckAsync(tab, mode.Slug, "selo-emitido");

        await tab.Page.Locator("#selo-guardado").FillAsync("selo alterado");
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Confirmar selo" }).ClickAsync();
        await Expect(tab.Page.Locator("#selo-guardado-erro")).ToBeVisibleAsync();
        await CheckAsync(tab, mode.Slug, "selo-recusado");
    }

    [Theory]
    [InlineData(PhoneWidth, PhoneHeight, "celular")]
    [InlineData(WideWidth, WideHeight, "1280")]
    public async Task TheScreenFitsTheWidthWithoutScrollingSidewaysAndHasNoViolationWithTheSealIssued(int width, int height, string slug)
    {
        await using var tab = await OpenAsync();
        await tab.Page.SetViewportSizeAsync(width, height);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Emitir selo" }).ClickAsync();
        await Expect(tab.Page.Locator("#selo-texto")).ToBeVisibleAsync();

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than {width} px.");
        await CheckAsync(tab, slug, "selo-emitido");
    }

    [Fact]
    public async Task TheKeyboardAloneIssuesTheSealAndRecordsItWithTheFocusSeen()
    {
        await using var tab = await OpenAsync();

        await FocusAsync(tab, "emitir-selo");
        Assert.True(await tab.Page.EvaluateAsync<bool>("() => document.activeElement.matches(':focus-visible') && getComputedStyle(document.activeElement).outlineStyle !== 'none'"));
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.Locator("#selo-texto")).ToBeVisibleAsync();
        var text = await tab.Page.Locator("#selo-texto").InputValueAsync();
        _ = await tab.Page.GotoAsync(SealScreenTests.Seal);
        await FocusAsync(tab, "selo-guardado");
        await tab.Page.Keyboard.InsertTextAsync(text);
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("O selo foi gravado na cadeia");
    }

    private static async Task FocusAsync(BrowserTab tab, string id)
    {
        for (var stop = 0; stop < HeaderStops.Count + 8 && !await tab.Page.EvaluateAsync<bool>($"() => document.activeElement.id === '{id}'"); stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
        }
    }

    private static async Task CheckAsync(BrowserTab tab, string slug, string screen)
    {
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, slug, screen);
    }

    private async Task<BrowserTab> OpenAsync()
    {
        var tab = await ScreenSession.SignedInAsync(browser, manual.Host, manual.Host.Administrator);
        try
        {
            _ = await tab.Page.GotoAsync(SealScreenTests.Seal);
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
