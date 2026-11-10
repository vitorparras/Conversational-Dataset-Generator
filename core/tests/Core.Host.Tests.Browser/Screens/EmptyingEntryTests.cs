using System.Globalization;
using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Core.Persistence.Content.Emptying;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.EmptyingPages;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The entry of the emptying of an item (issue 617, CA-1, CA-2 and CA-4) in a real Chromium, over the host with the real gates, the real routes and the real desk: one field, for the identifier,
/// that only a whole number above zero fills; no search by name, pseudonym or text anywhere; what will be emptied said for an item with its way on to the confirmation; and an item
/// that was emptied already told so, with the record of who executed it, when, why and for which protocol, and no way on to emptying it again.
/// </summary>
[Trait("Category", "Browser")]
public sealed class EmptyingEntryTests(BrowserFixture browser, EmptyingHostFixture emptying) : IClassFixture<EmptyingHostFixture>
{
    [Fact]
    public async Task Ca2TheScreenHasOneFieldForTheIdentifierAndNoSearchByNamePseudonymOrText()
    {
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = "Esvaziar item", Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("main input:not([type=hidden]), main textarea, main select")).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator("#item")).ToHaveAttributeAsync("inputmode", "numeric");
        await Expect(tab.Page.Locator("#item")).ToBeFocusedAsync();
        await Expect(tab.Page.GetByLabel("Identificador do item")).ToHaveCountAsync(1);
        await Expect(tab.Page.Locator("main [type=search], main [role=search], main [role=searchbox], main datalist, main [list]")).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator("main form")).ToHaveAttributeAsync("method", "get");
        Assert.DoesNotMatch(new Regex("buscar|pesquisar|procurar", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)), await tab.Page.Locator("main").InnerTextAsync());
        await Expect(tab.Page.Locator("nav.nav").GetByRole(AriaRole.Link, new() { Name = "Esvaziar item" })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task Ca1AnItemSaysWhatWillBeEmptiedAndOffersTheWayOnToTheConfirmationAndNothingElse()
    {
        var item = emptying.Host.NextItem();
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);

        await tab.Page.Locator("#item").FillAsync(item.ToString(CultureInfo.InvariantCulture));
        await tab.Page.Locator("#ver-item").ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Entry}\\?item={item}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#item-alvo-titulo")).ToHaveTextAsync($"Item {item}");
        await Expect(tab.Page.Locator(Target)).ToContainTextAsync("Ao esvaziar, o item perde os textos bruto, sanitizado e pseudonimizado e os mapas de reversão que tiver.");
        await Expect(tab.Page.Locator(Target)).ToContainTextAsync("Ficam o identificador, a marca de esvaziado e o registro.");
        var facts = await FactsAsync(tab.Page);
        Assert.Equal(["Domínio", "Estado"], facts.Keys);
        Assert.Equal(EmptyingScreenHost.Domain, facts["Domínio"]);
        Assert.EndsWith(NotEmptied, facts["Estado"], StringComparison.Ordinal);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Seguir para o esvaziamento" })).ToHaveAttributeAsync("href", ConfirmOf(item));
        await Expect(tab.Page.Locator("#item")).ToHaveValueAsync(item.ToString(CultureInfo.InvariantCulture));
        Assert.DoesNotMatch(@"\bmapas?\b.*\b\d+\b", await tab.Page.Locator(Target).InnerTextAsync());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12abc")]
    [InlineData("-3")]
    [InlineData("0")]
    [InlineData("1.5")]
    [InlineData("1 2")]
    [InlineData("99999999999999999999")]
    public async Task Ca2OnlyAWholeNumberAboveZeroIsAnIdentifierAnythingElseSendsTheFieldBackWithTheErrorBoundToItAndWhatWasTyped(string typed)
    {
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);

        await tab.Page.Locator("#item").FillAsync(typed);
        await tab.Page.Locator("#ver-item").ClickAsync();

        await Expect(tab.Page.Locator("#item-erro")).ToHaveTextAsync(NotANumber);
        await Expect(tab.Page.Locator("#item")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator("#item")).ToHaveAttributeAsync("aria-describedby", new Regex("item-erro", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#item")).ToHaveValueAsync(typed);
        await Expect(tab.Page.Locator(Target)).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Seguir para o esvaziamento" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheConfirmationOfAnIdentifierThatIsNotANumberSendsBackToTheEntryWithWhatWasTypedAndShowsNoConfirmation()
    {
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);

        _ = await tab.Page.GotoAsync($"{Confirm}?item=Maria");

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Entry}\\?item=Maria$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator("#item-erro")).ToHaveTextAsync(NotANumber);
        await Expect(tab.Page.Locator("#esvaziar")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca4AnItemAlreadyEmptiedIsToldSoWithTheRecordOfWhoExecutedItWhenWhyAndForWhichProtocolAndNoWayToEmptyItAgain()
    {
        var (item, protocol) = (emptying.Host.NextItem(), Protocol());
        await emptying.Host.EmptyAsync(item, EmptyingMotive.ContractRequirement, protocol);
        var before = DateTimeOffset.UtcNow;
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, EntryOf(item));

        await Expect(tab.Page.Locator($"{Target} .notice--info")).ToHaveTextAsync(new Regex("Este item já está esvaziado\\.$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        var facts = await FactsAsync(tab.Page);
        var at = DateTimeOffset.Parse(await tab.Page.Locator($"{Target} time").GetAttributeAsync("datetime") ?? string.Empty, CultureInfo.InvariantCulture);

        Assert.Equal(["Estado", "Quando", "Quem executou", "Motivo", "Quem pediu (protocolo)"], facts.Keys);
        Assert.Contains("Esvaziado", facts["Estado"], StringComparison.Ordinal);
        Assert.Equal(emptying.Host.Curator, facts["Quem executou"]);
        Assert.Equal(ByTheContractCaption, facts["Motivo"]);
        Assert.Equal(protocol, facts["Quem pediu (protocolo)"]);
        Assert.InRange(at, before.AddMinutes(-5), before.AddMinutes(1));
        await Expect(tab.Page.GetByRole(AriaRole.Link, new() { Name = "Seguir para o esvaziamento" })).ToHaveCountAsync(0);
        await Expect(tab.Page.Locator($"{Target} .notice--success")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca4TheAddressOfTheConfirmationOfAnItemAlreadyEmptiedLeadsToTheEntryThatSaysSo()
    {
        var item = emptying.Host.NextItem();
        await emptying.Host.EmptyAsync(item, EmptyingMotive.RemovalRequestedBySubject, Protocol());
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, Entry);

        _ = await tab.Page.GotoAsync(ConfirmOf(item));

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Entry}\\?item={item}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.Locator(".notice--info")).ToContainTextAsync("Este item já está esvaziado.");
        await Expect(tab.Page.Locator("#esvaziar")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheRecordNamesWhoExecutedItAndOnlyTheProtocolOfWhoAskedNeverTheirName()
    {
        var (item, protocol) = (emptying.Host.NextItem(), Protocol());
        await emptying.Host.EmptyAsync(item, EmptyingMotive.RemovalRequestedBySubject, protocol);
        await using var tab = await OpenAsync(browser, emptying.Host, emptying.Host.Curator, EntryOf(item));

        var facts = await FactsAsync(tab.Page);

        Assert.Equal(protocol, facts["Quem pediu (protocolo)"]);
        await Expect(tab.Page.Locator($"{Target} code")).ToHaveTextAsync(protocol);
        var page = await tab.Page.Locator(Target).InnerTextAsync();
        Assert.All([emptying.Host.Administrator, emptying.Host.Auditor, emptying.Host.Annotator], other => Assert.DoesNotContain(other, page, StringComparison.Ordinal));
    }
}
