using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the changes of configuration (A9, #634, part 2c) in a real Chromium, over the host with the real gates, routes and desks: a proposal goes through the rules of the start
/// and waits for another person (CA-1), the one who proposed never decides it (CA-2), another person of the technical administration approves it (CA-3), a key outside the list is not
/// offered (CA-5), and a person of another function finds the same page as an address that leads nowhere. The screen has no violation of WCAG 2.1 A and AA in the light and in the dark,
/// at 1280 px and on the width of a phone, and reads over the glass.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ConfigurationChangeScreenTests(BrowserFixture browser, ChangeHostFixture changes) : IClassFixture<ChangeHostFixture>
{
    private const string Route = "/configuracao/alteracoes";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int WideWidth = 1280;
    private const int WideHeight = 800;
    private const int NotFound = 404;

    [Fact]
    public async Task Ca1AProposalTheStartTakesIsSentAndWaitsForAnotherPerson()
    {
        await using var tab = await OpenAsync(changes.Host.Administrator);

        await ProposeAsync(tab.Page, "00:21:00");

        await Expect(tab.Page.GetByText("Alteração enviada.")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("#pendentes li", new() { HasText = "00:21:00" })).ToContainTextAsync("Proposta sua: outra pessoa decide");
    }

    [Fact]
    public async Task Ca1AValueTheStartWouldRefuseIsToldOnTheFieldOfTheValueAndNothingWaits()
    {
        await using var tab = await OpenAsync(changes.Host.Administrator);

        await ProposeAsync(tab.Page, "zqx-nao-e-tempo");

        await Expect(tab.Page.Locator("main")).ToContainTextAsync("A partida recusaria este valor");
        await Expect(tab.Page.Locator("#valor")).ToHaveValueAsync("zqx-nao-e-tempo");
        await Expect(tab.Page.Locator("#pendentes li", new() { HasText = "zqx-nao-e-tempo" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca2WhoProposedFindsNoButtonToDecideIt()
    {
        var id = await changes.Host.ProposeAsync("00:22:00");
        await using var tab = await OpenAsync(changes.Host.Administrator);

        await Expect(tab.Page.Locator($"#aprovar-{id}, #recusar-{id}")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca3AnotherPersonOfTheAdministrationApprovesAndTheProposalLeavesTheList()
    {
        var id = await changes.Host.ProposeAsync("00:23:00");
        await using var tab = await OpenAsync(changes.Host.SecondAdministrator);

        await tab.Page.Locator($"#aprovar-{id}").ClickAsync();

        await Expect(tab.Page.GetByText("Alteração aprovada.")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator($"#aprovar-{id}")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ADecisionOtherThanToApproveOrToRefuseLeadsNowhereAndTheProposalStillWaits()
    {
        var id = await changes.Host.ProposeAsync("00:26:00");
        await using var tab = await OpenAsync(changes.Host.SecondAdministrator);
        _ = await tab.Page.EvaluateAsync($"() => document.querySelector('#recusar-{id}').form.querySelector('[name=decisao]').value = 'zqx'");

        var answer = await tab.Page.RunAndWaitForResponseAsync(
            () => tab.Page.Locator($"#recusar-{id}").ClickAsync(), response => response.Request.Method == "POST");

        Assert.Equal(NotFound, answer.Status);
        _ = await tab.Page.GotoAsync(Route);
        await Expect(tab.Page.Locator($"#aprovar-{id}")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Ca5OnlyTheKeysOfTheListAreOfferedAndNoSecret()
    {
        await using var tab = await OpenAsync(changes.Host.Administrator);

        var offered = await tab.Page.Locator("#chave option").EvaluateAllAsync<string[]>("options => options.map(option => option.value).filter(value => value)");

        Assert.Equal(7, offered.Length);
        Assert.DoesNotContain(offered, key => key.StartsWith("Database:", StringComparison.Ordinal) || key.StartsWith("MasterKey:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APersonOfAnotherFunctionFindsAnAddressThatLeadsNowhere()
    {
        await using var tab = await ScreenSession.SignedInAsync(browser, changes.Host, changes.Host.Auditor);

        var answer = await tab.Page.GotoAsync(Route);

        Assert.Equal(NotFound, answer!.Status);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheScreenHasNoViolationOfWcagAAndAaAt1280AndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        _ = await changes.Host.ProposeAsync("00:24:00");
        await using var tab = await OpenAsync(changes.Host.SecondAdministrator);
        await tab.Page.SetViewportSizeAsync(WideWidth, WideHeight);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "alteracao-de-configuracao");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheScreenFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(changes.Host.Administrator);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        await mode.ApplyAsync(tab.Page);
        await ProposeAsync(tab.Page, "zqx-recusado");

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    private static async Task ProposeAsync(IPage page, string value)
    {
        _ = await page.Locator("#chave").SelectOptionAsync("Session:IdleTimeout");
        await page.Locator("#valor").FillAsync(value);
        _ = await page.Locator("#motivo").SelectOptionAsync("operational_adjustment");
        await page.Locator("#enviar-alteracao").ClickAsync();
    }

    private async Task<BrowserTab> OpenAsync(string name)
    {
        var tab = await ScreenSession.SignedInAsync(browser, changes.Host, name);
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
