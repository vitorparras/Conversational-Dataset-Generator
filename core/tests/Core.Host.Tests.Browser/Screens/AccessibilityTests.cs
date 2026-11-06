using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// Accessibility of the base page and of the trial screen (issue 591): axe with the rules of WCAG 2.1 A and AA in every state, the way through the page by the keyboard alone,
/// and the error of a field told by text, by shape and by the relation that a reader of screens follows.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class AccessibilityTests(BrowserFixture browser)
{
    private const string Focused =
        "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className || document.activeElement.tagName)";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const string FieldBorder = "() => { const s = getComputedStyle(document.getElementById('nome')); return s.borderTopStyle + ' ' + s.borderTopWidth; }";
    private const string ErrorMark = "() => getComputedStyle(document.getElementById('nome-erro'), '::before').content";

    private static readonly string[] Controls = ["skip-link", "header__name", "sair", "acionar", "nome", "enviar"];
    private static readonly string[] NoMark = ["none", "normal", "\"\""];

    [Fact]
    public async Task Ca6TheBasePageHasNoViolationOfWcagAAndAa()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Bare);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
    }

    [Fact]
    public async Task Ca6TheTrialScreenHasNoViolationOfWcagAAndAa()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
    }

    [Fact]
    public async Task Ca6TheTrialScreenHasNoViolationAfterTheSwapOfARegion()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        await tab.Page.Locator("#acionar").ClickAsync();
        await Expect(tab.Page.Locator("[data-status]")).ToHaveTextAsync(TrialRoutes.ShortcutMessage);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
    }

    [Fact]
    public async Task Ca6TheTrialScreenHasNoViolationWithTheErrorOfAFieldShown()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        await tab.Page.Locator("#enviar").ClickAsync();
        await Expect(tab.Page.Locator("#nome-erro")).ToBeVisibleAsync();

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
    }

    [Fact]
    public async Task Ca6TheKeyboardAloneReachesEveryControlInOrderWithTheFocusSeenAndLeavesTheLast()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < Controls.Length; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(Focused));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        await tab.Page.Keyboard.PressAsync("Tab");
        var beyond = await tab.Page.EvaluateAsync<string>(Focused);

        Assert.Equal(Controls, reached);
        Assert.DoesNotContain(false, seen);
        Assert.NotEqual(Controls[^1], beyond);
    }

    [Fact]
    public async Task Ca6TheKeyboardAloneWalksBackFromTheLastControlToTheFirst()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        await tab.Page.Locator("#enviar").FocusAsync();
        var back = new List<string>();

        for (var step = 1; step < Controls.Length; step++)
        {
            await tab.Page.Keyboard.PressAsync("Shift+Tab");
            back.Add(await tab.Page.EvaluateAsync<string>(Focused));
        }

        Assert.Equal(["nome", "acionar", "sair", "header__name", "skip-link"], back);
    }

    [Fact]
    public async Task Ca6TheSkipLinkMovesTheFocusToTheMainRegion()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);

        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.Locator("main")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Ca6TheErrorOfAFieldIsBoundToItAnnouncedAndTheFieldTakesTheFocus()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);

        await tab.Page.Locator("#enviar").ClickAsync();

        await Expect(tab.Page.Locator("#nome-erro")).ToHaveAttributeAsync("role", "alert");
        await Expect(tab.Page.Locator("#nome")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator("#nome")).ToHaveAttributeAsync("aria-describedby", "nome-erro");
        await Expect(tab.Page.Locator("#nome")).ToHaveAccessibleDescriptionAsync(NameRefusal());
        await Expect(tab.Page.Locator("#nome")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Ca6TheErrorStateDiffersWithoutColourByTheTextTheMarkAndTheBorder()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        var valid = await tab.Page.EvaluateAsync<string>(FieldBorder);

        await tab.Page.Locator("#enviar").ClickAsync();
        await Expect(tab.Page.Locator("#nome-erro")).ToHaveTextAsync(TrialRoutes.NameRefusal);

        var mark = await tab.Page.EvaluateAsync<string>(ErrorMark);
        Assert.NotEqual(valid, await tab.Page.EvaluateAsync<string>(FieldBorder));
        Assert.DoesNotContain(mark, NoMark);
    }

    [GeneratedRegex("Informe o nome")]
    private static partial Regex NameRefusal();
}
