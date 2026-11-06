using Microsoft.Playwright;

namespace Core.Host.Tests.Browser.Accessibility;

/// <summary>
/// The photographs of a screen in each setting of the system, for whoever reviews the design. They are made only when the variable names a folder, so that the run of the job leaves nothing
/// behind; the ones the pull request shows come from a run with the variable set, and live on a branch that never reaches the main.
/// </summary>
internal static class ModeCapture
{
    public const string Variable = "SCREEN_CAPTURES";

    public static async Task SaveAsync(IPage page, MediaMode mode, string screen)
    {
        var folder = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        _ = Directory.CreateDirectory(folder);
        _ = await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(folder, $"{screen}-{mode.Slug}.png"),
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled,
        });
    }
}
