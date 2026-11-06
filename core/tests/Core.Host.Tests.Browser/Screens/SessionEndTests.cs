using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// Ending the own sessions (issue 598, CA-3, decision 0051) in a real Chromium, over the host with the real gates and the real store of sessions: ending one, or all the others, is asked on a
/// page of its own that says what will end and never in a dialog of the browser; keeping them changes nothing; and what is ended stops being recognized by the server at once, while the one
/// who ended stays in. The two confirmations have no violation of WCAG 2.1 A and AA in the light, in the dark and on the width of a phone, read over the glass, and are reached and sent by the
/// keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SessionEndTests(BrowserFixture browser, AccountHostFixture account) : IClassFixture<AccountHostFixture>
{
    private const string Route = "/minha-conta";
    private const string Rows = "[aria-labelledby=sessoes-titulo] tbody tr";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;

    [Fact]
    public async Task Ca3TheConfirmationOfOneSessionSaysWhenItBeganAndWhenItWasLastUsedAndAsksOnAPageOfItsOwnNeverInADialog()
    {
        await using var tabs = await SessionsAsync(1);
        var dialogs = 0;
        tabs.Current.Page.Dialog += (_, _) => dialogs++;

        await tabs.Current.Page.Locator($"{Rows} a").ClickAsync();

        await Expect(tabs.Current.Page.GetByRole(AriaRole.Heading, new() { Name = "Encerrar sessão", Level = 1 })).ToBeVisibleAsync();
        await Expect(tabs.Current.Page).ToHaveURLAsync(new Regex(@"/minha-conta/sessoes/encerrar\?sessao=[^&]+$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tabs.Current.Page.Locator("dl > div:has(dt:text-is('Início')) time")).ToHaveCountAsync(1);
        await Expect(tabs.Current.Page.Locator("dl > div:has(dt:text-is('Última atividade')) time")).ToHaveCountAsync(1);
        await Expect(tabs.Current.Page.Locator("main")).ToContainTextAsync("Esta sessão deixa de valer na hora");
        await Expect(tabs.Current.Page.Locator("main")).ToContainTextAsync("A sessão em que você está continua aberta.");
        await Expect(tabs.Current.Page.Locator("main form")).ToHaveAttributeAsync("method", "post");
        await Expect(tabs.Current.Page.Locator("#encerrar-sessao")).ToHaveTextAsync("Encerrar sessão");
        await Expect(tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Manter sessão" })).ToHaveAttributeAsync("href", Route);
        _ = await tabs.Others[0].Page.GotoAsync(Route);
        await Expect(tabs.Others[0].Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
        Assert.Equal(0, dialogs);
    }

    [Fact]
    public async Task Ca3KeepingTheSessionEndsNothingAndTheOtherOneStillCounts()
    {
        await using var tabs = await SessionsAsync(1);
        await tabs.Current.Page.Locator($"{Rows} a").ClickAsync();

        await tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Manter sessão" }).ClickAsync();

        await Expect(tabs.Current.Page).ToHaveURLAsync(new Regex("/minha-conta$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tabs.Current.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        await Expect(tabs.Current.Page.Locator(Rows)).ToHaveCountAsync(2);
        _ = await tabs.Others[0].Page.GotoAsync(Route);
        await Expect(tabs.Others[0].Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Ca3AConfirmedEndingOfOneSessionMakesTheServerStopRecognizingItAtOnceAndTheCurrentOneStaysIn()
    {
        await using var tabs = await SessionsAsync(1);
        await tabs.Current.Page.Locator($"{Rows} a").ClickAsync();

        await tabs.Current.Page.Locator("#encerrar-sessao").ClickAsync();

        await Expect(tabs.Current.Page).ToHaveURLAsync(new Regex(@"/minha-conta\?feito=sessoes&encerradas=1$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tabs.Current.Page.Locator(".notice--success")).ToContainTextAsync("A sessão foi encerrada.");
        await Expect(tabs.Current.Page.Locator(Rows)).ToHaveCountAsync(1);
        await Expect(tabs.Current.Page.Locator(Rows)).ToContainTextAsync("Esta sessão");
        await Expect(tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Encerrar todas as outras" })).ToHaveCountAsync(0);
        await AssertNotRecognizedAsync(tabs.Others[0]);
    }

    [Fact]
    public async Task Ca3TheConfirmationOfAllTheOthersSaysHowManyThereAreAndEndingThemLeavesOnlyTheCurrentOne()
    {
        await using var tabs = await SessionsAsync(2);
        await tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Encerrar todas as outras" }).ClickAsync();

        await Expect(tabs.Current.Page.GetByRole(AriaRole.Heading, new() { Name = "Encerrar as outras sessões", Level = 1 })).ToBeVisibleAsync();
        await Expect(tabs.Current.Page.Locator("main")).ToContainTextAsync("As outras 2 sessões abertas deixam de valer na hora. A sessão em que você está continua aberta.");
        await Expect(tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Manter sessões" })).ToHaveAttributeAsync("href", Route);
        await tabs.Current.Page.Locator("#encerrar-outras").ClickAsync();

        await Expect(tabs.Current.Page).ToHaveURLAsync(new Regex(@"/minha-conta\?feito=sessoes&encerradas=2$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tabs.Current.Page.Locator(".notice--success")).ToContainTextAsync("2 sessões foram encerradas.");
        await Expect(tabs.Current.Page.Locator(Rows)).ToHaveCountAsync(1);
        await Expect(tabs.Current.Page.Locator("#encerrar-outras")).ToHaveCountAsync(0);
        foreach (var other in tabs.Others)
        {
            await AssertNotRecognizedAsync(other);
        }
    }

    [Fact]
    public async Task Ca3WithNoOtherSessionTheConfirmationSaysSoAndHasNothingToConfirm()
    {
        await using var tabs = await SessionsAsync(0);

        _ = await tabs.Current.Page.GotoAsync("/minha-conta/sessoes/encerrar-outras");

        await Expect(tabs.Current.Page.Locator("main")).ToContainTextAsync("Não há outras sessões abertas: só esta, que continua.");
        await Expect(tabs.Current.Page.Locator("main form, #encerrar-outras")).ToHaveCountAsync(0);
        await Expect(tabs.Current.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar à minha conta" })).ToHaveAttributeAsync("href", Route);
    }

    [Theory]
    [InlineData("Light", false)]
    [InlineData("Dark", false)]
    [InlineData("Light", true)]
    [InlineData("Dark", true)]
    public async Task TheConfirmationsHaveNoViolationOfWcagAAndAaAndReadOverTheGlass(string name, bool all)
    {
        var mode = MediaMode.Named(name);
        await using var tabs = await SessionsAsync(2);
        await ReachAsync(tabs.Current.Page, all);
        await mode.ApplyAsync(tabs.Current.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tabs.Current.Page);
        await RealBackgroundContrast.AssertAaAsync(tabs.Current.Page);
        await ModeCapture.SaveAsync(tabs.Current.Page, mode, all ? "encerrar-outras" : "encerrar-sessao");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheConfirmationsFitThePhoneWidthWithoutScrollingSidewaysAndHaveNoViolation(bool all)
    {
        await using var tabs = await SessionsAsync(2);
        await tabs.Current.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        await ReachAsync(tabs.Current.Page, all);

        var overflow = await tabs.Current.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tabs.Current.Page);
        await RealBackgroundContrast.AssertAaAsync(tabs.Current.Page);
        await ModeCapture.SaveAsync(tabs.Current.Page, "celular", all ? "encerrar-outras" : "encerrar-sessao");
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheConfirmationAndSendsItWithTheFocusSeenAndTheOtherSessionStopsCounting()
    {
        await using var tabs = await SessionsAsync(1);
        await TabToAsync(tabs.Current.Page, "() => document.activeElement.textContent.trim().startsWith('Encerrar a sessão iniciada em')");
        await tabs.Current.Page.Keyboard.PressAsync("Enter");
        await Expect(tabs.Current.Page.GetByRole(AriaRole.Heading, new() { Name = "Encerrar sessão", Level = 1 })).ToBeVisibleAsync();

        await TabToAsync(tabs.Current.Page, "() => document.activeElement.id === 'encerrar-sessao'");

        Assert.True(await tabs.Current.Page.EvaluateAsync<bool>(FocusIsSeen), "The button that ends the session shows no focus.");
        await tabs.Current.Page.Keyboard.PressAsync("Enter");
        await Expect(tabs.Current.Page).ToHaveURLAsync(new Regex(@"/minha-conta\?feito=sessoes&encerradas=1$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await AssertNotRecognizedAsync(tabs.Others[0]);
    }

    private static async Task TabToAsync(IPage page, string reached)
    {
        for (var stop = 0; stop < 10 && !await page.EvaluateAsync<bool>(reached); stop++)
        {
            await page.Keyboard.PressAsync("Tab");
        }

        Assert.True(await page.EvaluateAsync<bool>(reached), "The keyboard alone did not reach the control.");
    }

    private static async Task ReachAsync(IPage page, bool all)
    {
        if (all)
        {
            await page.GetByRole(AriaRole.Link, new() { Name = "Encerrar todas as outras" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Encerrar as outras sessões", Level = 1 })).ToBeVisibleAsync();
            return;
        }

        await page.Locator($"{Rows} a").First.ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Encerrar sessão", Level = 1 })).ToBeVisibleAsync();
    }

    private static async Task AssertNotRecognizedAsync(BrowserTab ended)
    {
        _ = await ended.Page.GotoAsync(Route);
        await Expect(ended.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToHaveCountAsync(0);
        await Expect(ended.Page.Locator("#sair")).ToHaveCountAsync(0);
    }

    /// <summary>The current session and the number of others of the same person that the test asks for, each in a context of its own.</summary>
    private async Task<Sessions> SessionsAsync(int others)
    {
        var person = await account.Host.AddPersonAsync();
        var opened = new List<BrowserTab>();
        try
        {
            for (var each = 0; each < others; each++)
            {
                opened.Add(await ScreenSession.SignedInAsync(browser, account.Host, person));
            }

            var current = await ScreenSession.SignedInAsync(browser, account.Host, person);
            opened.Add(current);
            _ = await current.Page.GotoAsync(Route);
            await Expect(current.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
            return new Sessions(current, [.. opened.Take(others)], opened);
        }
        catch
        {
            foreach (var tab in opened)
            {
                await tab.DisposeAsync();
            }

            throw;
        }
    }

    private sealed record Sessions(BrowserTab Current, IReadOnlyList<BrowserTab> Others, IReadOnlyList<BrowserTab> All) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var tab in All)
            {
                await tab.DisposeAsync();
            }
        }
    }
}
