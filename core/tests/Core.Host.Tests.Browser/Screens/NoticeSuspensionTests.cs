using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Core.Persistence.People;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.NoticePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The domains in read only when the chain does not conform (issue 595, CA-3, policy 005) in a real Chromium, over the host with the real gates, the real verification and a chain with the
/// second event changed outside the system: the screen of the domains says the writing is suspended and the reading goes on, with nothing to click and nothing of the link or the content;
/// every active person finds the same notice in the list, whatever the function, once however many verifications fail, and told again by the next failure after they read it; and with the
/// chain intact there is neither the warning nor the notice.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class NoticeSuspensionTests(BrowserFixture browser, BrokenNoticeHostFixture broken, SoundNoticeHostFixture sound)
    : IClassFixture<BrokenNoticeHostFixture>, IClassFixture<SoundNoticeHostFixture>
{
    [Fact]
    public async Task Ca3TheScreenOfTheDomainsSaysTheWritingIsSuspendedAndTheReadingGoesOnWithNothingToClick()
    {
        await using var tab = await OpenDomainsAsync(browser, broken.Host, broken.Host.Administrator);

        var warning = tab.Page.Locator("[role=alert].notice--error");

        await Expect(warning).ToHaveCountAsync(1);
        await Expect(warning).ToContainTextAsync(SuspendedBanner);
        Assert.Equal(NoticeScreenHost.Domains, await tab.Page.Locator("[aria-labelledby=dominios-titulo] tbody th[scope=row]").AllInnerTextsAsync());
        await Expect(tab.Page.Locator("main form, main input, main textarea, main select, main button")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca3TheWarningOfTheDomainsNamesNeitherTheLinkNorTheContentAndOffersNoLinkToClick()
    {
        await using var tab = await OpenDomainsAsync(browser, broken.Host, broken.Host.Administrator);

        var text = await tab.Page.Locator("[role=alert].notice--error").InnerTextAsync();

        await Expect(tab.Page.Locator(".notice a")).ToHaveCountAsync(0);
        Assert.DoesNotMatch(Detail(), text);
        Assert.DoesNotContain(NoticeScreenHost.BrokenSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca3WithTheChainIntactTheScreenOfTheDomainsHasNoWarningAndNobodyHasTheNotice()
    {
        var person = await sound.Host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await using var domains = await OpenDomainsAsync(browser, sound.Host, sound.Host.Administrator);
        await using var list = await OpenListAsync(browser, sound.Host, person.Name);

        await Expect(domains.Page.Locator(".notice--error, [role=alert]")).ToHaveCountAsync(0);
        await Expect(list.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum aviso não lido");
        Assert.Empty(await sound.Host.UnreadIdsOfAsync(person.Id));
    }

    [Theory]
    [InlineData("Audit")]
    [InlineData("Annotation")]
    public async Task Ca3EveryActivePersonFindsTheNoticeInTheListWithTheFixedWordsAndNoDetailWhateverTheFunction(string function)
    {
        var host = broken.Host;
        var person = await host.AddPersonAsync(function == "Audit" ? PersonFunctions.Audit : PersonFunctions.Accepted[0], TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);

        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        await Expect(WordsOfRows(tab.Page)).ToHaveTextAsync(SuspendedWords);
        await Expect(tab.Page.Locator($"{Rows} a")).ToHaveCountAsync(0);
        Assert.DoesNotMatch(Detail(), await WordsOfRows(tab.Page).InnerTextAsync());
        Assert.DoesNotContain(NoticeScreenHost.BrokenSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), await WordsOfRows(tab.Page).InnerTextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca3AFurtherFailedVerificationDoesNotTellThePersonASecondTimeWhileTheNoticeIsUnread()
    {
        var host = broken.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await host.VerifyAsync(TestContext.Current.CancellationToken);
        await host.VerifyAsync(TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);

        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        Assert.Equal((1, 0), await host.RecordOfAsync(person.Id));
    }

    [Fact]
    public async Task Ca4AfterTheNoticeIsReadItLeavesTheListKeepsTheRecordAndTheNextFailedVerificationTellsAgain()
    {
        var host = broken.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lido" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/avisos\?lido$"));
        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum aviso não lido");
        Assert.Equal((1, 1), await host.RecordOfAsync(person.Id));
        await host.VerifyAsync(TestContext.Current.CancellationToken);
        _ = await tab.Page.GotoAsync(List);
        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        Assert.Equal((2, 1), await host.RecordOfAsync(person.Id));
    }

    [GeneratedRegex(@"\b(evento|elo|elos|sequência|item|itens|hash|selo)\b|nº", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Detail();
}
