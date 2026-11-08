using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The skeleton of the screens and the menu in a real Chromium, over the host with the real gates and three people of different functions (issue 594): the start page with the menu
/// and without it, and the page of an address that leads nowhere, with no violation of WCAG 2.1 A and AA in the light, in the dark and on the width of a phone; the way through
/// them by the keyboard alone, with the skip link and the focus seen (CA-5); the menu made from the policy, in the order the screens declare (CA-3); a screen the function does not
/// reach answered as an address that does not exist (CA-2); and a person with no function that reaches anything seeing an empty start, with no menu and no error (CA-1).
/// </summary>
[Trait("Category", "Browser")]
public sealed class NavigationScreenTests(BrowserFixture browser, NavigationHostFixture navigation) : IClassFixture<NavigationHostFixture>
{
    private const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;
    private const int StatusNotFound = 404;

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca5TheStartWithTheMenuHasNoViolationOfWcagAAndAa(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await SignedInAsync(navigation.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        await Expect(tab.Page.Locator("nav.nav")).ToBeVisibleAsync();
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "inicio-com-menu");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca1TheStartOfWhoReachesNothingIsEmptyWithNoMenuNoSelectorNoErrorAndNoViolation(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await SignedInAsync(navigation.Host.Annotator);
        await mode.ApplyAsync(tab.Page);

        await Expect(tab.Page.Locator(".empty-state")).ToBeVisibleAsync();
        await Expect(tab.Page.GetByText("Nenhum domínio foi concedido a você")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("nav")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator("#dominio")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".notice--error, [role=alert]")).ToHaveCountAsync(0);
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "inicio-sem-menu");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca2TheAddressTheFunctionDoesNotReachHasThePageOfAnAddressThatLeadsNowhereWithNoViolation(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await SignedInAsync(navigation.Host.Annotator);
        await mode.ApplyAsync(tab.Page);
        var response = await tab.Page.GotoAsync(NavigationScreenHost.OfAdministration);

        Assert.Equal(StatusNotFound, response?.Status);
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Página não encontrada" })).ToBeVisibleAsync();
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "pagina-nao-encontrada");
    }

    [Fact]
    public async Task Ca5ThePagesFitThePhoneWidthWithoutScrollingSidewaysAndHaveNoViolation()
    {
        foreach (var (person, route, screen) in new[]
        {
            (navigation.Host.Administrator, "/", "inicio-com-menu"),
            (navigation.Host.Annotator, "/", "inicio-sem-menu"),
            (navigation.Host.Annotator, NavigationScreenHost.OfAdministration, "pagina-nao-encontrada"),
        })
        {
            await using var tab = await SignedInAsync(person);
            await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
            _ = await tab.Page.GotoAsync(route);

            var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

            Assert.True(overflow <= 0, $"The page {screen} is {overflow} px wider than the phone.");
            await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
            await RealBackgroundContrast.AssertAaAsync(tab.Page);
            await ModeCapture.SaveAsync(tab.Page, "celular", screen);
        }
    }

    [Fact]
    public async Task Ca5TheKeyboardAloneReachesTheHeaderTheMenuAndTheContentInOrderWithTheFocusSeen()
    {
        await using var tab = await SignedInAsync(navigation.Host.Administrator);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < HeaderStops.Count + 4; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(HeaderStops.Then("nav__link", "nav__link", "destinations__link", "destinations__link"), reached);
        Assert.DoesNotContain(false, seen);
    }

    [Fact]
    public async Task Ca5TheKeyboardAloneReachesTheHeaderOfAStartWithNoMenuAndLeavesTheLastControl()
    {
        await using var tab = await SignedInAsync(navigation.Host.Annotator);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < HeaderStops.Count; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        await tab.Page.Keyboard.PressAsync("Tab");
        var beyond = await tab.Page.EvaluateAsync<string>(FocusedId);

        Assert.Equal(HeaderStops.Signed, reached);
        Assert.DoesNotContain(false, seen);
        Assert.NotEqual(HeaderStops.Signed[^1], beyond);
    }

    [Fact]
    public async Task Ca5TheSkipLinkMovesTheFocusToTheContentOfAPageWithTheMenu()
    {
        await using var tab = await SignedInAsync(navigation.Host.Administrator);

        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.Locator("main")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Ca3TheMenuIsMadeOfTheScreensTheFunctionReachesInTheOrderTheyDeclareAndTheStartListsTheSame()
    {
        await using var administrator = await SignedInAsync(navigation.Host.Administrator);
        await using var auditor = await SignedInAsync(navigation.Host.Auditor);
        await using var annotator = await SignedInAsync(navigation.Host.Annotator);

        Assert.Equal([NavigationScreenHost.AdministrationCaption, NavigationScreenHost.AuditCaption], await administrator.Page.Locator("nav .nav__link").AllInnerTextsAsync());
        Assert.Equal([NavigationScreenHost.AdministrationCaption, NavigationScreenHost.AuditCaption], await administrator.Page.Locator(".destinations__link").AllInnerTextsAsync());
        Assert.Equal([NavigationScreenHost.AuditCaption], await auditor.Page.Locator("nav .nav__link").AllInnerTextsAsync());
        Assert.Equal([NavigationScreenHost.AuditCaption], await auditor.Page.Locator(".destinations__link").AllInnerTextsAsync());
        Assert.Empty(await annotator.Page.Locator("nav .nav__link").AllInnerTextsAsync());
        Assert.Empty(await annotator.Page.Locator(".destinations__link").AllInnerTextsAsync());
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca3TheMenuMarksTheScreenOfTheCurrentPageForTheEyeAndForAReaderOfScreens(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await SignedInAsync(navigation.Host.Administrator);
        await mode.ApplyAsync(tab.Page);

        _ = await tab.Page.GotoAsync(NavigationScreenHost.OfAudit);

        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = NavigationScreenHost.AuditCaption })).ToHaveAttributeAsync("aria-current", "page");
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = NavigationScreenHost.AdministrationCaption })).Not.ToHaveAttributeAsync("aria-current", "page");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "menu-pagina-atual");
    }

    [Theory]
    [InlineData(NavigationScreenHost.OfAdministration, "Auditor")]
    [InlineData(NavigationScreenHost.OfAnnotation, "Administrator")]
    [InlineData(NavigationScreenHost.OfAdministration, "Annotator")]
    public async Task Ca2AScreenTheFunctionDoesNotReachIsAnsweredAsAnAddressThatDoesNotExistAndTheAddressAskedIsNotInTheAnswer(string route, string who)
    {
        Assert.NotNull(route);
        var person = who switch
        {
            "Auditor" => navigation.Host.Auditor,
            "Administrator" => navigation.Host.Administrator,
            _ => navigation.Host.Annotator,
        };
        await using var tab = await SignedInAsync(person);

        var refused = await tab.Page.GotoAsync(route);
        var refusedPage = await tab.Page.ContentAsync();
        var missing = await tab.Page.GotoAsync(NavigationScreenHost.Missing);
        var missingPage = await tab.Page.ContentAsync();

        Assert.Equal((StatusNotFound, StatusNotFound), (refused?.Status, missing?.Status));
        Assert.Equal(missingPage, refusedPage);
        Assert.DoesNotContain(route.TrimStart('/'), refusedPage, StringComparison.Ordinal);
        Assert.DoesNotContain(NavigationScreenHost.Missing.TrimStart('/'), missingPage, StringComparison.Ordinal);
    }

    /// <summary>A page, in a context of its own, of a person who entered by the form, on the start page.</summary>
    private async Task<BrowserTab> SignedInAsync(string name)
    {
        var tab = await browser.OpenAtAsync(navigation.Host.Address, "/entrada");
        try
        {
            await tab.Page.Locator("#entrada-nome").FillAsync(name);
            await tab.Page.Locator("#entrada-senha").FillAsync(navigation.Host.Password);
            await tab.Page.Locator("#entrar").ClickAsync();
            await Expect(tab.Page.Locator("#sair")).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
