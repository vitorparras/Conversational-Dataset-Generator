using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Core.Persistence.People;
using static Core.Host.Tests.Browser.Screens.NoticePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The pages of the notices (issue 595) in a real Chromium, over the host with the real gates and routes: the list with notices, the list with none, the list with the notice that the writing
/// is suspended and the screen of the domains with its warning have no violation of WCAG 2.1 A and AA in the light, in the dark, with less transparency and more contrast, read over the glass,
/// fit the width of a phone without scrolling sideways, and the list is reached and its notices read by the keyboard alone, with the focus seen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class NoticeScreenLayoutTests(BrowserFixture browser, SoundNoticeHostFixture sound, BrokenNoticeHostFixture broken)
    : IClassFixture<SoundNoticeHostFixture>, IClassFixture<BrokenNoticeHostFixture>
{
    [Theory]
    [InlineData("avisos", "Light")]
    [InlineData("avisos", "Dark")]
    [InlineData("avisos", "ReducedTransparency")]
    [InlineData("avisos", "HighContrast")]
    [InlineData("avisos-nenhum", "Light")]
    [InlineData("avisos-nenhum", "Dark")]
    [InlineData("avisos-nenhum", "ReducedTransparency")]
    [InlineData("avisos-nenhum", "HighContrast")]
    [InlineData("avisos-dominio", "Light")]
    [InlineData("avisos-dominio", "Dark")]
    [InlineData("avisos-dominio", "ReducedTransparency")]
    [InlineData("avisos-dominio", "HighContrast")]
    [InlineData("dominios-suspenso", "Light")]
    [InlineData("dominios-suspenso", "Dark")]
    [InlineData("dominios-suspenso", "ReducedTransparency")]
    [InlineData("dominios-suspenso", "HighContrast")]
    public async Task ThePageHasNoViolationOfWcagAAndAaInTheFourModesAndItsTextReadsOverTheGlass(string page, string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenPageAsync(page);
        await mode.ApplyAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    [Theory]
    [InlineData("avisos")]
    [InlineData("avisos-nenhum")]
    [InlineData("avisos-dominio")]
    [InlineData("dominios-suspenso")]
    public async Task ThePagesFitThePhoneWidthWithoutScrollingSidewaysAndHaveNoViolation(string page)
    {
        await using var tab = await OpenPageAsync(page);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(page == "dominios-suspenso" ? DomainsRoute : List);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", page);
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheHeaderTheMenuTheTableAndEachButtonInOrderAndMarksANoticeWithTheFocusSeen()
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 2, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 4, TestContext.Current.CancellationToken);
        var unread = await host.UnreadIdsOfAsync(person.Id);
        await using var tab = await OpenListAsync(browser, host, person.Name);
        var reached = new List<string>();
        var seen = new List<bool>();

        for (var stop = 0; stop < 8; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            reached.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        }

        Assert.Equal(["skip-link", "header__name", "header__notices", "nav__link header__account", "sair", HeaderStops.TableStop, $"lido-{unread[0]}", $"lido-{unread[1]}"], reached);
        Assert.DoesNotContain(false, seen);
        await tab.Page.Keyboard.PressAsync("Shift+Tab");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/avisos\?lido$"));
        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        Assert.Equal([unread[1]], await host.UnreadIdsOfAsync(person.Id));
    }

    /// <summary>The page a name stands for, open for a person made for it, with what it shows made by the facts of its host.</summary>
    private async Task<BrowserTab> OpenPageAsync(string page)
    {
        var token = TestContext.Current.CancellationToken;
        if (page == "dominios-suspenso")
        {
            return await OpenDomainsAsync(browser, broken.Host, broken.Host.Administrator);
        }

        var host = page == "avisos-dominio" ? broken.Host : sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, token);
        if (page == "avisos")
        {
            await host.TellPasswordResetAsync(person.Id, 0, token);
            await host.TellPasswordResetAsync(person.Id, 1, token);
            await host.TellPasswordResetAsync(person.Id, 3, token);
        }

        return await OpenListAsync(browser, host, person.Name);
    }
}
