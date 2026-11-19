using Core.Host.Tests.Browser.Accessibility;
using Microsoft.Playwright;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>The checks of a screen in a mode, on the desktop and on the phone, shared by the screens of the batches and of the review by sample.</summary>
internal static class ScreenAccessibility
{
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";

    /// <summary>The checks of the screen in the mode given, on the desktop and on the phone, which must not scroll sideways.</summary>
    /// <param name="page">The screen, already loaded.</param>
    /// <param name="mode">The light or the dark.</param>
    /// <param name="capture">The name of the capture kept of the desktop.</param>
    public static async Task AssertAccessibleAsync(IPage page, MediaMode mode, string capture)
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
}
