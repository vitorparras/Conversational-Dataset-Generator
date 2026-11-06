using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The real entry and the real start page, in a real Chromium, over the host with the real gate, the real credentials and the real delay (issues 592 and 593): the entry in each of its
/// states and the start page with the header, with no violation of WCAG 2.1 A and AA in the light and in the dark; the waiting notice filled on the submit and the second submit not sent;
/// the way in and the way out by the keyboard alone, and the focus on the first field of the entry once the session is over.
/// </summary>
[Trait("Category", "Browser")]
public sealed class EntryScreenTests(BrowserFixture browser, EntryHostFixture entry) : IClassFixture<EntryHostFixture>
{
    private const string Entry = "/entrada";
    private const string Refusal = "Nome ou senha não conferem";
    private const string FocusedId = "() => document.activeElement.id || document.activeElement.className";

    /// <summary>What the page keeps, across the navigation, of each submit of the form: whether it was stopped, the busy mark and the waiting notice, once every listener had run.</summary>
    private const string SubmitRecorder =
        "document.addEventListener('submit', e => { setTimeout(() => { const f = e.target; const s = document.getElementById(f.getAttribute('data-wait'));" +
        " const l = JSON.parse(sessionStorage.getItem('submits') || '[]'); l.push([String(e.defaultPrevented), String(f.getAttribute('aria-busy')), s ? s.textContent : '']);" +
        " sessionStorage.setItem('submits', JSON.stringify(l)); }, 0); });";

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca6TheEntryHasNoViolationOfWcagAAndAaEmptyRefusedAndWaiting(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await browser.OpenAtAsync(entry.Host.Address, Entry);
        await mode.ApplyAsync(tab.Page);

        await Expect(tab.Page.Locator("#entrada-nome")).ToBeFocusedAsync();
        await Expect(tab.Page.GetByText("Cadastro vazio")).ToHaveCountAsync(0);
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "entrada-vazia");

        await FillAsync(tab.Page, entry.Host.Name, "zqx-senha-errada");
        await tab.Page.Locator("#entrar").ClickAsync();
        await Expect(tab.Page.GetByText(Refusal)).ToBeVisibleAsync();
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "entrada-recusa");

        _ = await tab.Page.GotoAsync(Entry);
        await FillAsync(tab.Page, entry.Host.Name, "zqx-senha-errada");
        _ = await tab.Page.EvaluateAsync("() => document.querySelector('form[data-wait]').addEventListener('submit', e => e.preventDefault())");
        await tab.Page.Locator("#entrar").ClickAsync();
        await Expect(tab.Page.Locator("#entrada-espera")).ToContainTextAsync("Conferindo");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "entrada-espera");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca6TheEntryOfAnEmptyRegistryHasNoViolationAndSaysSo(string name)
    {
        var mode = MediaMode.Named(name);
        await using var empty = await EntryScreenHost.StartAsync(withPerson: false, TestContext.Current.CancellationToken);
        await using var tab = await browser.OpenAtAsync(empty.Address, Entry);
        await mode.ApplyAsync(tab.Page);

        await Expect(tab.Page.GetByText("Cadastro vazio")).ToBeVisibleAsync();
        await Expect(tab.Page.GetByText("create-person")).ToBeVisibleAsync();
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "entrada-cadastro-vazio");
    }

    [Fact]
    public async Task Ca2TheWaitingNoticeIsFilledOnTheSubmitWithNoNumberAndTheSecondSubmitIsNotSent()
    {
        await using var tab = await browser.OpenAtAsync(entry.Host.Address, Entry);
        _ = await tab.Page.AddInitScriptAsync(SubmitRecorder);
        _ = await tab.Page.GotoAsync(Entry);
        var before = entry.Host.Posts;
        await FillAsync(tab.Page, entry.Host.Name, "zqx-senha-errada");

        entry.Host.HoldNextPost();
        var click = tab.Page.Locator("#entrar").ClickAsync();
        await WaitForPostsAsync(before + 1);
        await tab.Page.Keyboard.PressAsync("Enter");
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        var whileWaiting = entry.Host.Posts;
        entry.Host.Release();
        await click;

        await Expect(tab.Page.GetByText(Refusal)).ToBeVisibleAsync();
        var submits = JsonSerializer.Deserialize<string[][]>(await tab.Page.EvaluateAsync<string>("() => sessionStorage.getItem('submits')"));
        Assert.Equal(before + 1, whileWaiting);
        Assert.Equal(before + 1, entry.Host.Posts);
        Assert.NotNull(submits);
        Assert.Equal(2, submits.Length);
        Assert.Equal(["false", "true"], submits[0][..2]);
        Assert.Equal(["true", "true"], submits[1][..2]);
        Assert.Contains("Conferindo nome e senha", submits[0][2], StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\d", submits[0][2]);
        Assert.DoesNotContain(entry.Host.Name, submits[0][2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca3TheKeyboardAloneGoesInAndOutAndTheEntryOpensWithTheFocusOnTheFirstField()
    {
        await using var tab = await browser.OpenAtAsync(entry.Host.Address, Entry);
        await Expect(tab.Page.Locator("#entrada-nome")).ToBeFocusedAsync();
        await tab.Page.Keyboard.TypeAsync(entry.Host.Name);
        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.TypeAsync(entry.Host.Password);
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.Locator("#sair")).ToBeVisibleAsync();

        var reached = await TabToAsync(tab.Page, "sair");
        await tab.Page.Keyboard.PressAsync("Enter");

        Assert.True(reached, "The keyboard alone did not reach the exit in the header.");
        await Expect(tab.Page).ToHaveURLAsync(EntryAddress());
        await Expect(tab.Page.Locator("#entrada-nome")).ToBeFocusedAsync();
        _ = await tab.Page.GotoAsync("/");
        await Expect(tab.Page).ToHaveURLAsync(EntryAddress());
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ca6TheStartPageWithTheHeaderHasNoViolationAndTheKeyboardReachesTheExitInOrder(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await browser.OpenAtAsync(entry.Host.Address, Entry);
        await mode.ApplyAsync(tab.Page);
        await FillAsync(tab.Page, entry.Host.Name, entry.Host.Password);
        await tab.Page.Locator("#entrar").ClickAsync();
        await Expect(tab.Page.Locator("#sair")).ToBeVisibleAsync();

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "cabecalho-com-sair");
        var order = new List<string>();
        for (var stop = 0; stop < 4; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            order.Add(await tab.Page.EvaluateAsync<string>(FocusedId));
        }

        Assert.Equal(["skip-link", "header__name", "nav__link header__account", "sair"], order);
    }

    private static async Task FillAsync(IPage page, string name, string password)
    {
        await page.Locator("#entrada-nome").FillAsync(name);
        await page.Locator("#entrada-senha").FillAsync(password);
    }

    private static async Task<bool> TabToAsync(IPage page, string id)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            await page.Keyboard.PressAsync("Tab");
            if (await page.EvaluateAsync<string>(FocusedId) == id)
            {
                return true;
            }
        }

        return false;
    }

    private async Task WaitForPostsAsync(int expected)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(10);
        while (entry.Host.Posts < expected && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, entry.Host.Posts);
    }

    private Regex EntryAddress() => new($"^{Regex.Escape(entry.Host.Address.GetLeftPart(UriPartial.Authority))}{Entry}$");
}
