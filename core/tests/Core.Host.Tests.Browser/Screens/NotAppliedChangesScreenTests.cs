using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The approved changes that will not be applied, on the screen of the changes of configuration (issue 748), in a real Chromium: the person who administers sees the change as not applied with
/// the reason in words, from the closed list and with no value and no name of a person; and with the panel there the page has no violation of
/// WCAG 2.1 A and AA in the light and in the dark, at 1280 px and on the width of a phone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class NotAppliedChangesScreenTests(BrowserFixture browser, ChangeHostFixture changes) : IClassFixture<ChangeHostFixture>
{
    private const string Route = "/configuracao/alteracoes";
    private const string Panel = "[aria-labelledby=nao-aplicadas-titulo]";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int WideWidth = 1280;
    private const int WideHeight = 800;

    [Fact]
    public async Task AChangeApprovedAndNeverToBeAppliedIsShownAsNotAppliedWithTheReasonInWordsAndNoValueNorName()
    {
        var key = await changes.Host.RefuseAsync("approver_not_allowed");
        await using var tab = await OpenAsync();

        var item = tab.Page.Locator(Panel + " li").First;

        await Expect(item).ToContainTextAsync("Tempo de inatividade da sessão");
        await Expect(item).ToContainTextAsync(key);
        await Expect(item).ToContainTextAsync("Quem aprovou deixou de estar ativo na Administração técnica.");
        await Expect(item).ToContainTextAsync("Não aplicada");
        var text = await tab.Page.Locator(Panel).InnerTextAsync();
        Assert.DoesNotContain("00:25:00", text, StringComparison.Ordinal);
        Assert.DoesNotContain(changes.Host.Administrator, text, StringComparison.Ordinal);
        Assert.DoesNotContain(changes.Host.SecondAdministrator, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheScreenWithTheNotAppliedPanelHasNoViolationAt1280AndOnThePhoneWidth(string name)
    {
        var mode = MediaMode.Named(name);
        _ = await changes.Host.RefuseAsync("same_author");
        await using var tab = await OpenAsync();
        await mode.ApplyAsync(tab.Page);
        await tab.Page.SetViewportSizeAsync(WideWidth, WideHeight);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);
        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    private async Task<BrowserTab> OpenAsync()
    {
        var tab = await ScreenSession.SignedInAsync(browser, changes.Host, changes.Host.Administrator);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Alterar configuração", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
