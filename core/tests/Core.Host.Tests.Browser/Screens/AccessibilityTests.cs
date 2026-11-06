using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>Accessibility of the base page (issue 591): axe with the rules of WCAG 2.1 A and AA, and the skip link that takes the keyboard to the content.</summary>
[Trait("Category", "Browser")]
public sealed class AccessibilityTests(BrowserFixture browser)
{
    [Fact]
    public async Task Ca6TheBasePageHasNoViolationOfWcagAAndAa()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Bare);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
    }

    [Fact]
    public async Task Ca6TheSkipLinkMovesTheFocusToTheMainRegion()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Bare);

        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.Locator("main")).ToBeFocusedAsync();
    }
}
