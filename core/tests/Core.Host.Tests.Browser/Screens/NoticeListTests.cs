using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Core.Persistence.People;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.NoticePages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The notices of the person (issue 595, CA-2 and CA-4) in a real Chromium, over the host with the real gates, the real routes and the real act that sets a password again: the person
/// whose password was set again by another finds the notice with the number of sessions it ended, none, one or many, and the person who did it finds none; marking a notice as read, with
/// a post answered by a redirect, takes it from the list and keeps the record, and reloading the page does not mark again; with nothing to read the page says so; and the notice of another
/// person, one that does not exist, one already read and an identifier that is no number are answered, on the post, as for an address that does not exist.
/// </summary>
[Trait("Category", "Browser")]
public sealed class NoticeListTests(BrowserFixture browser, SoundNoticeHostFixture sound) : IClassFixture<SoundNoticeHostFixture>
{
    private const string NewPassword = "ZQX-nova-senha-5950";

    [Theory]
    [InlineData(0, "Nenhuma sessão estava aberta.")]
    [InlineData(1, "1 sessão aberta foi encerrada.")]
    [InlineData(3, "3 sessões abertas foram encerradas.")]
    public async Task Ca2ThePasswordSetAgainByAnotherIsToldWithTheNumberOfSessionsItEnded(int open, string told)
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        var sessions = new List<BrowserTab>();
        try
        {
            for (var session = 0; session < open; session++)
            {
                sessions.Add(await ScreenSession.SignedInAsync(browser, host, person.Name));
            }

            await using var administrator = await ScreenSession.SignedInAsync(browser, host, host.Administrator);
            await ResetPasswordAsync(administrator, person.Id, NewPassword);
            await using var again = await EnterAsync(browser, host, person.Name, NewPassword);

            _ = await again.Page.GotoAsync(List);

            await Expect(again.Page.Locator(Rows)).ToHaveCountAsync(1);
            await Expect(WordsOfRows(again.Page)).ToHaveTextAsync(ResetWords + told);
            Assert.NotEmpty((await again.Page.Locator($"{Rows} td").First.InnerTextAsync()).Trim());
            await Expect(again.Page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lido" })).ToHaveCountAsync(1);
            foreach (var ended in sessions)
            {
                _ = await ended.Page.GotoAsync(List);
                await Expect(ended.Page).ToHaveURLAsync(new Regex("/entrada$"));
            }
        }
        finally
        {
            foreach (var session in sessions)
            {
                await session.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Ca2WhoSetsThePasswordOfAnotherIsNotToldAndTheNoticeIsOnlyOfThePersonWhosePasswordItWas()
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await using var administrator = await ScreenSession.SignedInAsync(browser, host, host.Administrator);
        await ResetPasswordAsync(administrator, person.Id, NewPassword);

        _ = await administrator.Page.GotoAsync(List);

        await Expect(administrator.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum aviso não lido");
        _ = Assert.Single(await host.UnreadIdsOfAsync(person.Id));
    }

    [Fact]
    public async Task Ca4MarkingANoticeAsReadTakesItFromTheListKeepsTheRecordAndReloadingDoesNotMarkAgain()
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 2, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 5, TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);
        Assert.Equal([ResetWords + "5 sessões abertas foram encerradas.", ResetWords + "2 sessões abertas foram encerradas."], await WordsOfRows(tab.Page).AllInnerTextsAsync());

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lido" }).First.ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/avisos\?lido$"));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("O aviso foi marcado como lido.");
        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        await Expect(WordsOfRows(tab.Page)).ToHaveTextAsync(ResetWords + "2 sessões abertas foram encerradas.");
        _ = await tab.Page.ReloadAsync();
        await Expect(tab.Page.Locator(Rows)).ToHaveCountAsync(1);
        Assert.Equal((2, 1), await host.RecordOfAsync(person.Id));
    }

    [Fact]
    public async Task Ca4MarkingTheLastNoticeLeavesThePageSayingThereIsNoneNewAndTheRecordWhole()
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 1, TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lido" }).ClickAsync();

        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum aviso não lido");
        await Expect(tab.Page.Locator("table")).ToHaveCountAsync(0);
        Assert.Equal((1, 1), await host.RecordOfAsync(person.Id));
        Assert.Empty(await host.UnreadIdsOfAsync(person.Id));
    }

    [Fact]
    public async Task Ca4TheHeaderAndTheStartSayHowManyAreUnreadAndTheCountFallsAsEachIsMarkedUntilItIsGone()
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 2, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(person.Id, 5, TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);
        var count = tab.Page.Locator(".header__notices");

        await Expect(count).ToHaveTextAsync("2 avisos não lidos");
        await Expect(count).ToHaveAttributeAsync("href", List);
        _ = await tab.Page.GotoAsync("/");
        await Expect(tab.Page.Locator(".notice a[href='/avisos']")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("main .notice")).ToContainTextAsync("Você tem 2 avisos não lidos.");
        _ = await tab.Page.GotoAsync(List);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lido" }).First.ClickAsync();
        await Expect(count).ToHaveTextAsync("1 aviso não lido");
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lido" }).ClickAsync();

        await Expect(count).ToHaveCountAsync(0);
        _ = await tab.Page.GotoAsync("/");
        await Expect(tab.Page.Locator("main a[href='/avisos']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca4WithNoNoticeThePageSaysSoAndOffersNothingToDo()
    {
        var host = sound.Host;
        var person = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await using var tab = await OpenListAsync(browser, host, person.Name);

        await Expect(tab.Page.Locator(".empty-state")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".empty-state__title")).ToHaveTextAsync("Nenhum aviso não lido");
        await Expect(tab.Page.Locator("main form, main button, main table")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".notice--error, [role=alert]")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator(".header__notices")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("OfAnother")]
    [InlineData("DoesNotExist")]
    [InlineData("NotANumber")]
    public async Task Ca4ANoticeThatIsNotTheirsOrIsGoneIsAnsweredOnThePostAsForAnAddressThatDoesNotExist(string which)
    {
        var host = sound.Host;
        var owner = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        var reader = await host.AddPersonAsync(PersonFunctions.Audit, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(owner.Id, 4, TestContext.Current.CancellationToken);
        await host.TellPasswordResetAsync(reader.Id, 1, TestContext.Current.CancellationToken);
        var ofOwner = Assert.Single(await host.UnreadIdsOfAsync(owner.Id));
        await using var tab = await ScreenSession.SignedInAsync(browser, host, reader.Name);
        var identifier = which switch
        {
            "OfAnother" => ofOwner.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "DoesNotExist" => "999999999",
            _ => "zqx-aviso",
        };
        var posted = await PostAsync(tab, identifier);
        var missing = await tab.Page.Context.APIRequest.GetAsync(new Uri(new Uri(tab.Page.Url), Missing).AbsoluteUri, new() { Headers = Html() });

        Assert.Equal((StatusNotFound, StatusNotFound), (posted.Status, missing.Status));
        Assert.Equal(await missing.TextAsync(), await posted.TextAsync());
        Assert.Equal([ofOwner], await host.UnreadIdsOfAsync(owner.Id));
    }

    private static Dictionary<string, string> Html() => new() { ["Accept"] = "text/html" };

    private static Task<IAPIResponse> PostAsync(BrowserTab tab, string identifier) =>
        tab.Page.Context.APIRequest.PostAsync(
            new Uri(new Uri(tab.Page.Url), $"{List}/{identifier}/lido").AbsoluteUri,
            new() { Headers = Html(), Form = tab.Page.Context.APIRequest.CreateFormData(), MaxRedirects = 0 });
}
