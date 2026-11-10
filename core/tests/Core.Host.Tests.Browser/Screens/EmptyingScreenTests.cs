using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Persistence.Content.Emptying;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.EmptyingPages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screens of the emptying (issue 617) as the person at them meets them, in a real Chromium: no violation of WCAG 2.1 A and AA in the light, in the dark, with less transparency and more
/// contrast, on the entry, on the entry with an item, on the entry with an item emptied, on the confirmation and on the confirmation refused; the text reading over the glass; the width of a
/// phone without scrolling sideways; and the whole of it, from the identifier to the emptying, reached and sent by the keyboard alone with the focus always seen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class EmptyingScreenTests(BrowserFixture browser, EmptyingHostFixture emptying) : IClassFixture<EmptyingHostFixture>
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheEntryWithAnItemHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, EntryOf(emptying.Host.NextItem()));
        await mode.ApplyAsync(tab.Page);

        await CheckAsync(tab.Page, mode, "esvaziamento");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheEmptyEntryAndTheEntryWithAnErrorHaveNoViolationOfWcagAAndAa(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);
        await mode.ApplyAsync(tab.Page);
        await CheckAsync(tab.Page, mode, "esvaziamento-vazio");

        _ = await tab.Page.GotoAsync($"{Entry}?item=Maria");
        await Expect(tab.Page.Locator("#item-erro")).ToBeVisibleAsync();

        await CheckAsync(tab.Page, mode, "esvaziamento-erro");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrast")]
    public async Task TheEntryWithAnItemAlreadyEmptiedHasNoViolationOfWcagAAndAaAndTheRecordReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        var item = emptying.Host.NextItem();
        await emptying.Host.EmptyAsync(item, EmptyingMotive.RemovalRequestedBySubject, Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, EntryOf(item));
        await mode.ApplyAsync(tab.Page);

        await CheckAsync(tab.Page, mode, "esvaziamento-esvaziado");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheConfirmationHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(emptying.Host.NextItem()));
        await mode.ApplyAsync(tab.Page);

        await CheckAsync(tab.Page, mode, "confirmacao");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheConfirmationRefusedHasNoViolationOfWcagAAndAaAndTheErrorsReadOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(emptying.Host.NextItem()));
        await mode.ApplyAsync(tab.Page);
        await FillAsync(tab.Page, string.Empty, "Maria da Silva");
        _ = await SendAsync(tab.Page);
        await Expect(tab.Page.Locator("fieldset.choices .field__error")).ToBeVisibleAsync();

        await CheckAsync(tab.Page, mode, "confirmacao-recusa");
    }

    [Fact]
    public async Task TheScreensFitThePhoneWidthWithoutScrollingSidewaysAndHaveNoViolation()
    {
        var item = emptying.Host.NextItem();
        var emptied = emptying.Host.NextItem();
        await emptying.Host.EmptyAsync(emptied, EmptyingMotive.ContractRequirement, Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        foreach (var (route, screen) in new[] { (EntryOf(item), "esvaziamento"), (EntryOf(emptied), "esvaziamento-esvaziado"), (ConfirmOf(item), "confirmacao") })
        {
            _ = await tab.Page.GotoAsync(route);
            var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

            Assert.True(overflow <= 0, $"The page {screen} is {overflow} px wider than the phone.");
            await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
            await RealBackgroundContrast.AssertAaAsync(tab.Page);
            await ModeCapture.SaveAsync(tab.Page, "celular", screen);
        }
    }

    [Fact]
    public async Task TheKeyboardAloneTypesTheIdentifierSeesTheItemReachesTheWayOnAndTheConfirmationWithTheFocusSeen()
    {
        var item = emptying.Host.NextItem();
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);
        var order = new List<string>();
        var seen = new List<bool>();

        await Expect(tab.Page.Locator("#item")).ToBeFocusedAsync();
        await tab.Page.Keyboard.TypeAsync($"{item}");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.Locator(Target)).ToBeVisibleAsync();
        await tab.Page.Locator("#item").FocusAsync();
        for (var stop = 0; stop < 2; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            order.Add(await tab.Page.EvaluateAsync<string>(Focused));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(["ver-item", "Seguir para o esvaziamento"], order);
        Assert.DoesNotContain(false, seen);
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Confirm}\\?item={item}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task TheKeyboardAloneChoosesTheMotiveTypesTheProtocolReachesTheDangerButtonWithTheFocusSeenAndEmpties()
    {
        var (item, protocol) = (emptying.Host.NextItem(), Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));
        var order = new List<string>();
        var seen = new List<bool>();

        await tab.Page.Locator($"input[name=motivo][value={ByTheSubject}]").FocusAsync();
        await tab.Page.Keyboard.PressAsync("ArrowDown");
        await Expect(tab.Page.Locator($"input[name=motivo][value={ByTheContract}]")).ToBeCheckedAsync();
        for (var stop = 0; stop < 3; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            order.Add(await tab.Page.EvaluateAsync<string>(Focused));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
            await tab.Page.Keyboard.TypeAsync(stop == 0 ? protocol : string.Empty);
        }

        Assert.Equal(["protocolo", "esvaziar", "Não esvaziar"], order);
        Assert.DoesNotContain(false, seen);
        await tab.Page.Keyboard.PressAsync("Shift+Tab");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Entry}\\?item={item}&feito=1$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        _ = Assert.Single(await emptying.Host.EventsOfAsync(item));
    }

    private static async Task CheckAsync(IPage page, MediaMode mode, string screen)
    {
        await AccessibilityCheck.AssertNoViolationsAsync(page);
        await RealBackgroundContrast.AssertAaAsync(page);
        await ModeCapture.SaveAsync(page, mode, screen);
    }
}
