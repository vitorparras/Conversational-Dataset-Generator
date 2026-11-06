using System.Globalization;
using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Core.Host.Tests.Browser.Accessibility;

/// <summary>
/// The check every test of a screen calls: axe over the page as it stands, with the rules of WCAG 2.1 levels A and AA, and no violation accepted.
/// A screen is not proved accessible by this alone; what a tool cannot see is on the manual review list (docs/arquitetura/022).
/// </summary>
internal static class AccessibilityCheck
{
    private static readonly string[] Tags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

    public static async Task AssertNoViolationsAsync(IPage page)
    {
        var options = new AxeRunOptions { RunOnly = new RunOnlyOptions { Type = "tag", Values = [.. Tags] } };
        var results = await page.RunAxe(options);
        Assert.True(results.Violations.Length == 0, Describe(results.Violations));
    }

    private static string Describe(IEnumerable<AxeResultItem> violations)
    {
        var text = new StringBuilder("axe found violations of WCAG 2.1 A/AA:");
        foreach (var violation in violations)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{Environment.NewLine}- {violation.Id} ({violation.Impact}): {violation.Help}");
            foreach (var node in violation.Nodes)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"{Environment.NewLine}    {node.Html}");
            }
        }

        return text.ToString();
    }
}
