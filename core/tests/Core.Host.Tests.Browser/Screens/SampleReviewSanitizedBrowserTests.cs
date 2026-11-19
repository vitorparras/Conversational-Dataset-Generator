using System.Globalization;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.ScreenAccessibility;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The review by sample of a batch whose texts were sanitized (issue 98, CA-3) in a real Chromium, over the real host and the real service of the submissions: each item shows its sanitized
/// text, with the typed marker where the credential was, and the screen has no violation of WCAG 2.1 A and AA in the light and in the dark, at 1280 px and on a phone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SampleReviewSanitizedBrowserTests(BrowserFixture browser, SampleReviewSanitizedHostFixture sanitized) : IClassFixture<SampleReviewSanitizedHostFixture>
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task SampleReviewOfSanitizedTextPassesTheAccessibilityCheck(string name)
    {
        var tag = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        var batch = await SampleReviewRealCase.OpenBatchAsync(sanitized.Host, tag);
        await using var tab = await ScreenSession.SignedInAsync(browser, sanitized.Host, sanitized.Host.Curator);
        var number = batch.ToString(CultureInfo.InvariantCulture);
        _ = await tab.Page.GotoAsync($"/lotes/{number}/revisao");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Revisão da amostra do lote {number}", Level = 1 })).ToBeVisibleAsync();

        await tab.Page.Locator("#sortear").ClickAsync();

        var texts = tab.Page.Locator(".sample__text");
        await Expect(texts).ToHaveCountAsync(SampleReviewRealCase.Items);
        foreach (var text in await texts.AllInnerTextsAsync())
        {
            Assert.StartsWith("[credencial]QXTEXTO" + tag + "ITEM", text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ZQXTEXTO", await tab.Page.ContentAsync(), StringComparison.Ordinal);
        await AssertAccessibleAsync(tab.Page, MediaMode.Named(name), "revisao-sanitizada");
    }
}
