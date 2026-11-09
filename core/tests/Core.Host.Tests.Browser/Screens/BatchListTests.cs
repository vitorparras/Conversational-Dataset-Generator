using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the batches (issue 608) in a real Chromium, over the host with the real gates and the real route, once with a domain granted and once with none: the batches of the
/// domain granted alone, with the domain and who submitted (CA-1); the filter by state in the address and cut by the server (CA-1); with no domain granted, the list empty with the
/// motive and no entry in the menu (CA-3); and any other function answered as for an address that does not exist. The screen has no violation of WCAG 2.1 A and AA in the light and in
/// the dark, and fits the width of a phone and of 1280 px without scrolling sideways.
/// </summary>
[Trait("Category", "Browser")]
public sealed class BatchListTests(BrowserFixture browser, GrantedBatchesHostFixture granted, UngrantedBatchesHostFixture ungranted)
    : IClassFixture<GrantedBatchesHostFixture>, IClassFixture<UngrantedBatchesHostFixture>
{
    private const string Route = "/lotes";
    private const string Missing = "/zqx-nao-existe-6081";
    private const string Rows = "[aria-labelledby=lotes-titulo] tbody tr";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const int StatusNotFound = 404;

    [Fact]
    public async Task Ca1TheBatchesOfTheDomainGrantedAloneAreListedWithTheDomainAndWhoSubmitted()
    {
        await using var tab = await OpenAsync(granted.Host);

        Assert.Equal(["Lote 1"], await tab.Page.Locator($"{Rows} th[scope=row]").AllInnerTextsAsync());
        await Expect(tab.Page.Locator($"{Rows} td").Nth(0)).ToHaveTextAsync(BatchScreenHost.Granted);
        await Expect(tab.Page.Locator($"{Rows} td").Nth(2)).ToHaveTextAsync("zqx-seed");
        await Expect(tab.Page.Locator("main")).Not.ToContainTextAsync(BatchScreenHost.Other);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Lotes" })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task Ca1TheFilterByStateGoesInTheAddressAndTheServerCutsTheList()
    {
        await using var tab = await OpenAsync(granted.Host);

        await tab.Page.GetByLabel("Rejeitados").CheckAsync();
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Filtrar" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("estado=rejeitados"));
        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum lote com esse filtro.");
        await Expect(tab.Page.GetByLabel("Rejeitados")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Ca3WithNoDomainGrantedTheCurationSeesTheListEmptyWithTheMotiveAndNoEntryInTheMenu()
    {
        await using var tab = await OpenAsync(ungranted.Host);

        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum lote para mostrar.");
        await Expect(tab.Page.Locator(".empty-state")).ToContainTextAsync("Você não tem concessão em nenhum domínio.");
        await Expect(tab.Page.Locator("main form, main table")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Lotes" })).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    [InlineData("Annotator")]
    public async Task AnyOtherFunctionIsAnsweredAsForAnAddressThatDoesNotExist(string who)
    {
        var host = granted.Host;
        var person = who switch
        {
            "Administrator" => host.Administrator,
            "Auditor" => host.Auditor,
            _ => host.Annotator,
        };
        await using var tab = await ScreenSession.SignedInAsync(browser, host, person);

        var answer = await tab.Page.GotoAsync(Route);
        var page = await tab.Page.ContentAsync();
        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();

        Assert.Equal((StatusNotFound, StatusNotFound), (answer?.Status, missing?.Status));
        Assert.Equal(missingPage.Replace(Missing.TrimStart('/'), string.Empty, StringComparison.Ordinal), page.Replace(Route.TrimStart('/'), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("Lote 1", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Light", true)]
    [InlineData("Dark", true)]
    [InlineData("Light", false)]
    [InlineData("Dark", false)]
    public async Task TheScreenHasNoViolationOfWcagAAndAaInTheLightAndInTheDark(string name, bool withGrant)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(withGrant ? granted.Host : ungranted.Host);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, withGrant ? "lotes" : "lotes-sem-concessao");
    }

    [Theory]
    [InlineData(390, 844, true)]
    [InlineData(390, 844, false)]
    [InlineData(1280, 800, true)]
    public async Task TheScreenFitsThePhoneAndTheDesktopWithoutScrollingSidewaysAndHasNoViolation(int width, int height, bool withGrant)
    {
        await using var tab = await OpenAsync(withGrant ? granted.Host : ungranted.Host);
        await tab.Page.SetViewportSizeAsync(width, height);
        _ = await tab.Page.GotoAsync(Route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than {width} px.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, width < 1280 ? "celular" : "1280", withGrant ? "lotes" : "lotes-sem-concessao");
    }

    private async Task<BrowserTab> OpenAsync(BatchScreenHost host)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, host.Curator);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Lotes", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
