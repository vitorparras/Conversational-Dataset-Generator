using System.Diagnostics;
using Core.Host.Tests.Browser.Fixtures;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The delay of the refusals of the change of the own password (issue 597, CA-1): a wrong current password counts in the delay of the entry, so that from the second refusal on the answer
/// waits, with the waiting notice on the page, and says what every refusal says. The host is the one of this class alone, because the delay counts by the origin of the request.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PasswordDelayTests(BrowserFixture browser, AccountHostFixture account) : IClassFixture<AccountHostFixture>
{
    private const string Route = "/minha-conta/senha";
    private const int MilliSecondsOfTheFirstDelay = 900;

    /// <summary>What the page keeps, across the navigation, of each submit of the form: whether it was stopped, the busy mark and the waiting notice, once every listener had run.</summary>
    private const string SubmitRecorder =
        "document.addEventListener('submit', e => { setTimeout(() => { const f = e.target; const s = document.getElementById(f.getAttribute('data-wait'));" +
        " const l = JSON.parse(sessionStorage.getItem('submits') || '[]'); l.push([String(e.defaultPrevented), String(f.getAttribute('aria-busy')), s ? s.textContent : '']);" +
        " sessionStorage.setItem('submits', JSON.stringify(l)); }, 0); });";

    [Fact]
    public async Task Ca1ARefusalCountsInTheDelayOfTheEntryAndTheAnswerWaitsWithTheNoticeOnThePageAndSaysWhatEveryRefusalSays()
    {
        var host = account.Host;
        await using var tab = await ScreenSession.SignedInAsync(browser, host, await host.AddPersonAsync());
        _ = await tab.Page.AddInitScriptAsync(SubmitRecorder);
        _ = await tab.Page.GotoAsync(Route);
        var waits = new List<long>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await PasswordScreenTests.FillAsync(tab.Page, PasswordScreenTests.Made("ERRADA"), PasswordScreenTests.Made("NOVA"));
            var started = Stopwatch.StartNew();
            await tab.Page.Locator("#senha-nova").FocusAsync();
            await tab.Page.Keyboard.PressAsync("Enter");
            await Expect(tab.Page.GetByText(PasswordScreenTests.Refusal)).ToBeVisibleAsync();
            await Expect(tab.Page.Locator("#senha-atual")).ToHaveValueAsync(string.Empty);
            waits.Add(started.ElapsedMilliseconds);
        }

        var submits = await tab.Page.EvaluateAsync<string[][]>("() => JSON.parse(sessionStorage.getItem('submits') || '[]')");
        Assert.Equal(3, submits.Length);
        Assert.All(submits, submit => Assert.Equal(["false", "true"], submit[..2]));
        Assert.All(submits, submit => Assert.Contains("Trocando a senha", submit[2], StringComparison.Ordinal));

        Assert.True(waits[1] >= MilliSecondsOfTheFirstDelay, $"The second refusal answered with no delay: {string.Join(", ", waits)} ms.");
        Assert.True(waits[2] > waits[1], $"The delay did not grow: {string.Join(", ", waits)} ms.");
    }
}
