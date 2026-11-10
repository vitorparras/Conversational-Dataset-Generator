using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screen of the seal (issue 736, CA-1 to CA-4, decision 0051) in a real Chromium, over the host with the real gates, the real routes and the real chain: the technical administration
/// issues the seal and sees its text and where to keep it, with nothing of it in the address; records the seal it kept, which enters the chain and takes the missing-seal warning away, or has
/// a seal altered come back with the reason beside the field; whoever does not hold the function is answered as for an address that does not exist, on the page and on both posts; and an
/// installation where the host sends the seal says so and offers no action.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SealScreenTests(BrowserFixture browser, ManualSealHostFixture manual, SentSealHostFixture sent) : IClassFixture<ManualSealHostFixture>, IClassFixture<SentSealHostFixture>
{
    internal const string Seal = "/custodia/selo";
    private const string Issue = "/custodia/selo/emitir";
    private const string Confirm = "/custodia/selo/confirmar";
    private const string Missing = "/zqx-nao-existe-7360";
    private const string Text = "#selo-texto";
    private const string Kept = "#selo-guardado";
    private const int StatusNotFound = 404;
    private const int StatusOk = 200;

    [Fact]
    public async Task Ca1TheIssuedSealShowsItsTextAndWhereToKeepItWithNothingOfItInTheAddressAndNothingOnTheNextVisit()
    {
        await using var tab = await OpenAsync(manual.Host, manual.Host.Administrator);

        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Emitir selo" }).ClickAsync();

        await Expect(tab.Page.GetByText("Guarde este texto onde quem controla a base não alcance.")).ToBeVisibleAsync();
        var text = await tab.Page.Locator(Text).InputValueAsync();
        Assert.Contains("|", text, StringComparison.Ordinal);
        Assert.DoesNotContain(text, tab.Page.Url, StringComparison.Ordinal);
        await Expect(tab.Page.Locator(Text)).ToHaveAttributeAsync("readonly", string.Empty);
        _ = await tab.Page.GotoAsync(Seal);
        await Expect(tab.Page.Locator(Text)).ToHaveCountAsync(0);
        Assert.DoesNotContain(text, await tab.Page.ContentAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(text, manual.Host.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca2TheSealKeptWithTheSameTextEntersTheChainAndTheMissingSealWarningGoesAway()
    {
        await using var tab = await OpenAsync(manual.Host, manual.Host.Administrator);
        await Expect(tab.Page.Locator(".notice--pending")).ToContainTextAsync("Nenhum selo foi registrado");
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Emitir selo" }).ClickAsync();
        var text = await tab.Page.Locator(Text).InputValueAsync();
        _ = await tab.Page.GotoAsync(Seal);

        await tab.Page.Locator(Kept).FillAsync(text);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Confirmar selo" }).ClickAsync();

        await Expect(tab.Page).ToHaveURLAsync(new Regex(@"/custodia/selo\?registrado$"));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync("O selo foi gravado na cadeia");
        await Expect(tab.Page.Locator(".notice--pending").Filter(new() { HasText = "selo" })).ToHaveCountAsync(0);
        _ = await tab.Page.GotoAsync("/custodia");
        await Expect(tab.Page.Locator(".notice--pending").Filter(new() { HasText = "selo" })).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByText("Nenhum selo registrado")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ca2ASealWithTheTextAlteredComesBackWithTheReasonBesideTheFieldAndWhatWasTyped()
    {
        await using var tab = await OpenAsync(manual.Host, manual.Host.Administrator);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Emitir selo" }).ClickAsync();
        var altered = (await tab.Page.Locator(Text).InputValueAsync()) + "0";
        _ = await tab.Page.GotoAsync(Seal);

        await tab.Page.Locator(Kept).FillAsync(altered);
        await tab.Page.GetByRole(AriaRole.Button, new() { Name = "Confirmar selo" }).ClickAsync();

        await Expect(tab.Page.Locator("#selo-guardado-erro")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(Kept)).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator(Kept)).ToHaveAttributeAsync("aria-describedby", new Regex("selo-guardado-erro"));
        await Expect(tab.Page.Locator(Kept)).ToHaveValueAsync(altered);
        Assert.DoesNotContain("registrado", tab.Page.Url, StringComparison.Ordinal);
        Assert.DoesNotContain(altered, manual.Host.Log, StringComparison.Ordinal);
        Assert.DoesNotContain(altered[..^1], manual.Host.Log, StringComparison.Ordinal);
        await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("Auditor")]
    [InlineData("Curator")]
    [InlineData("Annotator")]
    public async Task Ca3WhoDoesNotHoldTheFunctionIsAnsweredAsForAnAddressThatDoesNotExistOnThePageAndOnBothPosts(string who)
    {
        var host = manual.Host;
        await using var tab = await ScreenSession.SignedInAsync(browser, host, Named(host, who));

        var refused = await tab.Page.GotoAsync(Seal);
        var refusedPage = await tab.Page.ContentAsync();
        var missing = await tab.Page.GotoAsync(Missing);
        var missingPage = await tab.Page.ContentAsync();
        var issued = await PostAsync(tab, Issue);
        var confirmed = await PostAsync(tab, Confirm);

        Assert.Equal((StatusNotFound, StatusNotFound, StatusNotFound, StatusNotFound), (refused?.Status, missing?.Status, issued.Status, confirmed.Status));
        Assert.Equal(missingPage.Replace(Missing.TrimStart('/'), string.Empty, StringComparison.Ordinal), refusedPage.Replace(Seal.TrimStart('/'), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("emitir", refusedPage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ca4WhereTheHostSendsTheSealThePageSaysSoAndOffersNoAction()
    {
        await using var tab = await OpenAsync(sent.Host, sent.Host.Administrator);

        await Expect(tab.Page.GetByText("o núcleo envia o selo sozinho")).ToBeVisibleAsync();
        await Expect(tab.Page.Locator("main form")).ToHaveCountAsync(0);
        await Expect(tab.Page.GetByRole(AriaRole.Button, new() { Name = "Emitir selo" })).ToHaveCountAsync(0);
        var posted = await PostAsync(tab, Issue);
        Assert.Equal(StatusOk, posted.Status);
        Assert.DoesNotContain("selo-texto", await posted.TextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheChainScreenLeadsTheAdministrationToTheSealAndNeverTheAuditNorAnInstallationThatSendsIt()
    {
        await using var administration = await ScreenSession.SignedInAsync(browser, manual.Host, manual.Host.Administrator);
        await using var audit = await ScreenSession.SignedInAsync(browser, manual.Host, manual.Host.Auditor);
        await using var automatic = await ScreenSession.SignedInAsync(browser, sent.Host, sent.Host.Administrator);

        _ = await administration.Page.GotoAsync("/custodia");
        _ = await audit.Page.GotoAsync("/custodia");
        _ = await automatic.Page.GotoAsync("/custodia");

        await Expect(administration.Page.Locator("#selo-emitir")).ToHaveAttributeAsync("href", Seal);
        await Expect(audit.Page.Locator("#selo-emitir")).ToHaveCountAsync(0);
        await Expect(automatic.Page.Locator("#selo-emitir")).ToHaveCountAsync(0);
    }

    private static string Named(ScreenHostBase host, string who) => who switch
    {
        "Auditor" => host.Auditor,
        "Curator" => host.Curator,
        _ => host.Annotator,
    };

    private static async Task<IAPIResponse> PostAsync(BrowserTab tab, string route) =>
        await tab.Page.Context.APIRequest.PostAsync(
            new Uri(new Uri(tab.Page.Url), route).AbsoluteUri,
            new() { Headers = new Dictionary<string, string> { ["Accept"] = "text/html" }, Form = tab.Page.Context.APIRequest.CreateFormData() });

    private async Task<BrowserTab> OpenAsync(ScreenHostBase host, string name)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, name);
        try
        {
            _ = await tab.Page.GotoAsync(Seal);
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }
}
