using System.Globalization;
using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.EmptyingPages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The confirmation and the act of the emptying (issue 617, CA-1, CA-3 and CA-4) in a real Chromium, over the host with the real gates, the real routes, the real desk and the real emptier: the
/// confirmation is a page of its own, with no dialog of the browser, that says what is lost and asks for the motive and the protocol; the act marks the item as emptied, records who asked, who
/// executed, when and why, and adds an event to the chain; what the form got wrong comes back on the form with what was typed and empties nothing; and an item no one has comes back on the entry.
/// </summary>
[Trait("Category", "Browser")]
public sealed class EmptyingActTests(BrowserFixture browser, EmptyingHostFixture emptying) : IClassFixture<EmptyingHostFixture>
{
    [Fact]
    public async Task Ca1TheConfirmationIsAPageOfItsOwnWithNoDialogThatSaysWhatIsLostAndAsksForTheMotiveAndTheProtocol()
    {
        var item = emptying.Host.NextItem();
        var dialogs = 0;
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, EntryOf(item));
        tab.Page.Dialog += async (_, dialog) =>
        {
            dialogs++;
            await dialog.DismissAsync();
        };

        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Seguir para o esvaziamento" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Confirm}\\?item={item}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Confirmar o esvaziamento", Level = 1 })).ToBeVisibleAsync();
        var loss = $"O item {item} do domínio {EmptyingScreenHost.Domain} perde os textos bruto, sanitizado e pseudonimizado e os mapas de reversão que tiver, de uma vez e sem volta.";
        await Expect(tab.Page.Locator("main > p").First).ToContainTextAsync(loss);
        await Expect(tab.Page.Locator("main > p").First).ToContainTextAsync("Ficam o identificador, a marca de esvaziado e o registro de quem executou, quando e por quê.");
        Assert.Equal([ByTheSubjectCaption, ByTheContractCaption], (await tab.Page.Locator("fieldset.choices label").AllInnerTextsAsync()).Select(text => text.Trim()));
        await Expect(tab.Page.Locator("fieldset.choices legend")).ToContainTextAsync("Motivo");
        await Expect(tab.Page.Locator("input[name=motivo]:checked")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByLabel("Protocolo do pedido")).ToHaveAttributeAsync("name", "protocolo");
        await Expect(tab.Page.Locator("#esvaziar")).ToHaveTextAsync($"Esvaziar o item {item}");
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Não esvaziar" })).ToHaveAttributeAsync("href", EntryOf(item));
        await Expect(tab.Page.Locator("main form")).ToHaveAttributeAsync("method", "post");
        Assert.Equal(0, dialogs);
    }

    [Fact]
    public async Task Ca3EmptyingMarksTheItemAndRecordsWhoExecutedItWhenWhyAndForWhichProtocolAndTheChainGainsOneEvent()
    {
        var (item, protocol) = (emptying.Host.NextItem(), Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));
        await FillAsync(tab.Page, ByTheSubject, protocol);
        var chainBefore = await emptying.Host.ChainLengthAsync();
        var before = DateTimeOffset.UtcNow;

        var response = await SendAsync(tab.Page);

        Assert.Equal(303, response.Status);
        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Entry}\\?item={item}&feito=1$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator($"{Target} .notice--success")).ToContainTextAsync("O item foi esvaziado.");
        var facts = await FactsAsync(tab.Page);
        var at = DateTimeOffset.Parse(await tab.Page.Locator($"{Target} time").GetAttributeAsync("datetime") ?? string.Empty, CultureInfo.InvariantCulture);
        Assert.Contains("Esvaziado", facts["Estado"], StringComparison.Ordinal);
        Assert.Equal((emptying.Host.Curator, ByTheSubjectCaption, protocol), (facts["Quem executou"], facts["Motivo"], facts["Quem pediu (protocolo)"]));
        Assert.InRange(at, before.AddMinutes(-5), before.AddMinutes(1));
        Assert.Equal([$"item_emptied|removal_requested_by_subject|{protocol}|{emptying.Host.Curator}"], await emptying.Host.EventsOfAsync(item));
        Assert.Equal(chainBefore + 1, await emptying.Host.ChainLengthAsync());
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Seguir para o esvaziamento" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca4AfterTheEmptyingTheEntryOfTheSameItemSaysItIsEmptiedAlreadyAndAnotherEmptyingChangesNothing()
    {
        var (item, protocol) = (emptying.Host.NextItem(), Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));
        await FillAsync(tab.Page, ByTheContract, protocol);
        _ = await SendAsync(tab.Page);

        _ = await tab.Page.GotoAsync(EntryOf(item));
        await Expect(tab.Page.Locator(".notice--info")).ToContainTextAsync("Este item já está esvaziado.");
        var again = await tab.Page.Context.APIRequest.PostAsync(
            new Uri(new Uri(tab.Page.Url), Confirm).AbsoluteUri,
            new() { Form = Asked(tab.Page, item), MaxRedirects = 0 });

        Assert.Equal(303, again.Status);
        var facts = await FactsAsync(tab.Page);
        Assert.Equal((ByTheContractCaption, protocol), (facts["Motivo"], facts["Quem pediu (protocolo)"]));
        _ = Assert.Single(await emptying.Host.EventsOfAsync(item), line => line.StartsWith("item_emptied|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMotiveNotChosenSendsTheConfirmationBackWithTheErrorBoundToTheGroupTheProtocolAsTypedAndEmptiesNothing()
    {
        var (item, protocol) = (emptying.Host.NextItem(), Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));
        await FillAsync(tab.Page, string.Empty, protocol);

        var response = await SendAsync(tab.Page);

        Assert.Equal(200, response.Status);
        await Expect(tab.Page.Locator("fieldset.choices .field__error")).ToHaveTextAsync("Escolha o motivo do esvaziamento.");
        await Expect(tab.Page.Locator("fieldset.choices .field__error")).ToHaveAttributeAsync("role", "alert");
        await Expect(tab.Page.Locator("input[name=motivo][aria-invalid=true]")).ToHaveCountAsync(2);
        await Expect(tab.Page.Locator("#protocolo")).ToHaveValueAsync(protocol);
        await Expect(tab.Page.Locator("#esvaziar")).ToHaveTextAsync($"Esvaziar o item {item}");
        Assert.Empty(await emptying.Host.EventsOfAsync(item));
        _ = await tab.Page.GotoAsync(EntryOf(item));
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Seguir para o esvaziamento" })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("Maria da Silva")]
    [InlineData("")]
    [InlineData("PROT 2026")]
    [InlineData("ZQX-0000000000000000000000000000000000000000000000000000000000000000X")]
    public async Task AProtocolThatIsNotANumberOfTheKindAcceptedSendsTheConfirmationBackWithTheErrorBoundToItTheMotiveAsChosenAndEmptiesNothing(string protocol)
    {
        var item = emptying.Host.NextItem();
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));
        await FillAsync(tab.Page, ByTheContract, protocol);

        _ = await SendAsync(tab.Page);

        await Expect(tab.Page.Locator("#protocolo-erro")).ToHaveTextAsync("O protocolo aceita só letras, dígitos e . _ / -, até 64 caracteres.");
        await Expect(tab.Page.Locator("#protocolo")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator("#protocolo")).ToHaveValueAsync(protocol);
        await Expect(tab.Page.Locator($"input[name=motivo][value={ByTheContract}]")).ToBeCheckedAsync();
        Assert.Empty(await emptying.Host.EventsOfAsync(item));
    }

    [Theory]
    [InlineData(EmptyingScreenHost.MissingItem)]
    [InlineData(EmptyingScreenHost.ItemOfOtherDomain)]
    public async Task AnItemTheDomainDoesNotHaveComesBackOnTheEntryWithTheErrorBoundToTheFieldAndNothingIsRecorded(int item)
    {
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));
        await FillAsync(tab.Page, ByTheSubject, Protocol());
        var chainBefore = await emptying.Host.ChainLengthAsync();

        _ = await SendAsync(tab.Page);

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Confirm}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#item-erro")).ToHaveTextAsync(Absent);
        await Expect(tab.Page.Locator("#item")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator("#item")).ToHaveValueAsync(item.ToString(CultureInfo.InvariantCulture));
        await Expect(tab.Page.Locator(Target)).ToHaveCountAsync(0);
        Assert.Empty(await emptying.Host.EventsOfAsync(item));
        Assert.Equal(chainBefore, await emptying.Host.ChainLengthAsync());
    }

    [Fact]
    public async Task NotToEmptyLeadsBackToTheEntryOfTheItemWhichIsStillNotEmptied()
    {
        var item = emptying.Host.NextItem();
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, ConfirmOf(item));

        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Não esvaziar" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Entry}\\?item={item}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator(Target)).ToContainTextAsync(NotEmptied);
        Assert.Empty(await emptying.Host.EventsOfAsync(item));
    }

    private static IFormData Asked(IPage page, int item) =>
        page.Context.APIRequest.CreateFormData().Set("item", item.ToString(CultureInfo.InvariantCulture)).Set("motivo", ByTheSubject).Set("protocolo", "ZQX-OUTRO");
}
