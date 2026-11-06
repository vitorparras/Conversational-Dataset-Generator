using Core.Host.Tests.Browser.Fixtures;
using Core.Host.Tests.Browser.Trial;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The shortcut the markup declares (decision 0010): the key fires its effect with the focus outside a field, and does nothing while the person types. A box of a choice, checkbox or
/// radio, is no field to type in (issue 671, CA-5): the test marks it with the action of checking, never with the one of typing, and the key still fires.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ShortcutTests(BrowserFixture browser)
{
    [Fact]
    public async Task Ca3TheKeyFiresTheEffectWithTheFocusOutsideAField()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);

        await tab.Page.Keyboard.PressAsync("a");

        await Expect(tab.Page.Locator("[data-status]")).ToHaveTextAsync(TrialRoutes.ShortcutMessage);
    }

    [Fact]
    public async Task Ca3TheKeyTypedInATextFieldFiresNothingAndTheFieldReceivesIt()
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Trial);
        var fired = new List<string>();
        tab.Page.Request += (_, request) =>
        {
            if (request.Url.EndsWith(TrialRoutes.Shortcut, StringComparison.Ordinal))
            {
                fired.Add(request.Url);
            }
        };

        await tab.Page.Locator("#nome").PressSequentiallyAsync("aaa");
        await tab.Page.Locator("h1").ClickAsync();
        await tab.Page.Keyboard.PressAsync("a");

        await Expect(tab.Page.Locator("[data-status]")).ToHaveTextAsync(TrialRoutes.ShortcutMessage);
        Assert.Equal(("aaa", 1), (await tab.Page.Locator("#nome").InputValueAsync(), fired.Count));
    }

    [Theory]
    [InlineData("aceito")]
    [InlineData("opcao-a")]
    public async Task Ca5TheKeyFiresTheEffectWithTheFocusOnABoxMarkedByTheCheckActionAndTheBoxStaysMarked(string id)
    {
        await using var tab = await browser.OpenAsync(TrialRoutes.Choice);

        await tab.Page.Locator($"#{id}").CheckAsync();
        await Expect(tab.Page.Locator($"#{id}")).ToBeCheckedAsync();
        await tab.Page.Keyboard.PressAsync("a");

        await Expect(tab.Page.Locator("[data-status]")).ToHaveTextAsync(TrialRoutes.ShortcutMessage);
        await Expect(tab.Page.Locator($"#{id}")).ToBeCheckedAsync();
    }
}
