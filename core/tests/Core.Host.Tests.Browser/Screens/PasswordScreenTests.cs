using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The change of the own password (issue 597, decisions 0029 and 0051) in a real Chromium, over the host with the real gates, the real routes and the real change: a wrong current password
/// refused with the one text of every refusal (CA-1); a change accepted that ends the other sessions of the person while the current one stays in, and that tells how many (CA-2); and no
/// password, old or new, in the page, in the address or in the log of the host, neither after a refusal nor after a change. The page has no violation of WCAG 2.1 A and AA in the light, in the
/// dark, with less transparency and more contrast, in the state of a refusal and on the width of a phone, and is reached and sent by the keyboard alone. The delay of the refusals is in
/// <see cref="PasswordDelayTests"/>, which has a host of its own, because the delay counts by the origin and every test here comes from the same one.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PasswordScreenTests(BrowserFixture browser, AccountHostFixture account) : IClassFixture<AccountHostFixture>
{
    internal const string Refusal = "A senha atual não confere. Confira e tente de novo.";
    private const string Route = "/minha-conta/senha";
    private const string Account = "/minha-conta";
    private const string EntryRefusal = "Nome ou senha não conferem";
    private const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    private const string FocusedId = "() => document.activeElement.id || document.activeElement.textContent.trim()";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;

    [Fact]
    public async Task ThePageAsksForTheCurrentAndTheNewPasswordWithTheRightAutocompleteAndTheFocusOnTheFirstField()
    {
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Trocar senha", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("#senha-atual")).ToBeFocusedAsync();
        await Expect(tab.Page.Locator("#senha-atual")).ToHaveAttributeAsync("type", "password");
        await Expect(tab.Page.Locator("#senha-atual")).ToHaveAttributeAsync("autocomplete", "current-password");
        await Expect(tab.Page.Locator("#senha-nova")).ToHaveAttributeAsync("type", "password");
        await Expect(tab.Page.Locator("#senha-nova")).ToHaveAttributeAsync("autocomplete", "new-password");
        await Expect(tab.Page.Locator("main form")).ToHaveAttributeAsync("method", "post");
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar à minha conta" })).ToHaveAttributeAsync("href", Account);
    }

    [Fact]
    public async Task Ca1AWrongCurrentPasswordIsRefusedWithTheTextOfEveryRefusalAndNothingChangesNorIsWrittenBack()
    {
        var host = account.Host;
        var person = await host.AddPersonAsync();
        var wrong = Made("ERRADA");
        var replacement = Made("NOVA");
        await using var tab = await OpenAsync(person);

        await FillAsync(tab.Page, wrong, replacement);
        await tab.Page.Locator("#trocar-senha").ClickAsync();

        await Expect(tab.Page.GetByText(Refusal)).ToBeVisibleAsync();
        await Expect(tab.Page).ToHaveURLAsync(new Regex("/minha-conta/senha$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#senha-atual")).ToHaveValueAsync(string.Empty);
        await Expect(tab.Page.Locator("#senha-nova")).ToHaveValueAsync(string.Empty);
        await Expect(tab.Page.Locator("#senha-repetida")).ToHaveValueAsync(string.Empty);
        await Expect(tab.Page.Locator("#senha-atual")).ToHaveAttributeAsync("aria-invalid", "true");
        await AssertNoPasswordAsync(tab.Page, host.Log, wrong, replacement);
        await AssertCanEnterAsync(host, person, host.Password);
    }

    [Fact]
    public async Task TwoNewPasswordsThatDifferAreRefusedBesideTheRepeatFieldAndNothingChanges()
    {
        var host = account.Host;
        var person = await host.AddPersonAsync();
        var replacement = Made("NOVA");
        await using var tab = await OpenAsync(person);

        await tab.Page.Locator("#senha-atual").FillAsync(host.Password);
        await tab.Page.Locator("#senha-nova").FillAsync(replacement);
        await tab.Page.Locator("#senha-repetida").FillAsync(Made("OUTRA"));
        await tab.Page.Locator("#trocar-senha").ClickAsync();

        await Expect(tab.Page.Locator("#senha-repetida-erro")).ToContainTextAsync("As duas senhas novas não são iguais");
        await Expect(tab.Page.Locator("#senha-repetida")).ToHaveAttributeAsync("aria-invalid", "true");
        await AssertCanEnterAsync(host, person, host.Password);
    }

    [Fact]
    public async Task AnEmptyNewPasswordIsRefusedWithTheTextThatSaysSoAndNothingChanges()
    {
        var host = account.Host;
        var person = await host.AddPersonAsync();
        await using var tab = await OpenAsync(person);

        await FillAsync(tab.Page, host.Password, string.Empty);
        await tab.Page.Locator("#trocar-senha").ClickAsync();

        await Expect(tab.Page.GetByText("Informe a nova senha.")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("#senha-atual")).ToHaveValueAsync(string.Empty);
        await AssertNoPasswordAsync(tab.Page, host.Log, host.Password);
        await AssertCanEnterAsync(host, person, host.Password);
    }

    [Fact]
    public async Task Ca2AChangeAcceptedEndsTheOtherSessionsAtOnceKeepsTheCurrentOneAndTellsHowManyEndedWithNoPasswordAnywhere()
    {
        var host = account.Host;
        var person = await host.AddPersonAsync();
        var replacement = Made("NOVA");
        await using var other = await ScreenSession.SignedInAsync(browser, host, person);
        await using var tab = await OpenAsync(person);
        await FillAsync(tab.Page, host.Password, replacement);

        await tab.Page.Locator("#trocar-senha").ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex("/minha-conta$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Senha trocada. A outra sessão aberta foi encerrada; esta continua aberta.");
        await Expect(tab.Page.Locator("[aria-labelledby=sessoes-titulo] tbody tr")).ToHaveCountAsync(1);
        await AssertNoPasswordAsync(tab.Page, host.Log, host.Password, replacement);
        _ = await tab.Page.GotoAsync(Account);
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToBeVisibleAsync();
        _ = await other.Page.GotoAsync(Account);
        await Expect(other.Page.GetByRole(AriaRole.Heading, new() { Name = "Minha conta", Level = 1 })).ToHaveCountAsync(0);
        await Expect(other.Page.Locator("#sair")).ToHaveCountAsync(0);
        await AssertCanEnterAsync(host, person, replacement);
        await AssertCannotEnterAsync(host, person, host.Password);
    }

    [Fact]
    public async Task Ca2AChangeAcceptedWithNoOtherSessionSaysThereWasNoneToEnd()
    {
        var host = account.Host;
        var person = await host.AddPersonAsync();
        await using var tab = await OpenAsync(person);
        await FillAsync(tab.Page, host.Password, Made("NOVA"));

        await tab.Page.Locator("#trocar-senha").ClickAsync();

        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Senha trocada. Não havia outras sessões abertas.");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task ThePageHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "senha");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task ThePageAfterARefusalHasNoViolationOfWcagAAndAaAndTheErrorReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());
        await mode.ApplyAsync(tab.Page);
        await FillAsync(tab.Page, Made("ERRADA"), Made("NOVA"));
        await tab.Page.Locator("#trocar-senha").ClickAsync();
        await Expect(tab.Page.GetByText(Refusal)).ToBeVisibleAsync();

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "senha-recusa");
    }

    [Fact]
    public async Task ThePageFitsThePhoneWidthWithoutScrollingSidewaysAndHasNoViolation()
    {
        await using var tab = await OpenAsync(await account.Host.AddPersonAsync());
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "senha");
    }

    [Fact]
    public async Task TheKeyboardAloneFillsTheThreeFieldsReachesTheButtonWithTheFocusSeenAndSendsTheChange()
    {
        var host = account.Host;
        await using var tab = await OpenAsync(await host.AddPersonAsync());
        var replacement = Made("NOVA");
        var order = new List<string>();
        var seen = new List<bool>();

        // The first field takes the focus from its autofocus, which can come after the heading is seen on a loaded machine; a key typed before that is lost, and the password goes cut and is refused.
        await Expect(tab.Page.Locator("#senha-atual")).ToBeFocusedAsync();
        await tab.Page.Keyboard.TypeAsync(host.Password);
        for (var stop = 0; stop < 4; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            order.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
            if (stop <= 1)
            {
                await tab.Page.Keyboard.TypeAsync(replacement);
            }
        }

        Assert.Equal(["senha-nova", "senha-repetida", "trocar-senha", "Voltar à minha conta"], order);
        Assert.DoesNotContain(false, seen);
        await tab.Page.Keyboard.PressAsync("Shift+Tab");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex("/minha-conta$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("Senha trocada. Não havia outras sessões abertas.");
    }

    internal static string Made(string kind) => $"ZQX-SENHA-{kind}-{Guid.NewGuid():N}";

    internal static async Task FillAsync(IPage page, string current, string replacement)
    {
        await page.Locator("#senha-atual").FillAsync(current);
        await page.Locator("#senha-nova").FillAsync(replacement);
        await page.Locator("#senha-repetida").FillAsync(replacement);
    }

    private static async Task AssertNoPasswordAsync(IPage page, string log, params string[] passwords)
    {
        var seen = string.Join('\n', page.Url, await page.ContentAsync(), log);
        Assert.All(passwords.Where(password => password.Length > 0), password => Assert.DoesNotContain(password, seen, StringComparison.Ordinal));
        Assert.All(passwords.Where(password => password.Length > 0), password => Assert.DoesNotContain(Uri.EscapeDataString(password), seen, StringComparison.Ordinal));
    }

    private async Task AssertCanEnterAsync(ScreenHostBase host, string person, string password)
    {
        await using var tab = await browser.OpenAtAsync(host.Address, "/entrada");
        await tab.Page.Locator("#entrada-nome").FillAsync(person);
        await tab.Page.Locator("#entrada-senha").FillAsync(password);
        await tab.Page.Locator("#entrar").ClickAsync();
        await Expect(tab.Page.Locator("#sair")).ToBeVisibleAsync();
    }

    private async Task AssertCannotEnterAsync(ScreenHostBase host, string person, string password)
    {
        await using var tab = await browser.OpenAtAsync(host.Address, "/entrada");
        await tab.Page.Locator("#entrada-nome").FillAsync(person);
        await tab.Page.Locator("#entrada-senha").FillAsync(password);
        await tab.Page.Locator("#entrar").ClickAsync();
        await Expect(tab.Page.GetByText(EntryRefusal)).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("#sair")).ToHaveCountAsync(0);
    }

    private async Task<BrowserTab> OpenAsync(string person)
    {
        var tab = await ScreenSession.SignedInAsync(browser, account.Host, person);
        try
        {
            _ = await tab.Page.GotoAsync(Route);
            await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Trocar senha", Level = 1 })).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
