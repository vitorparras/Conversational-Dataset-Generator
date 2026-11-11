using System.Globalization;
using System.Text.RegularExpressions;
using Core.Host.Screens;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Core.Host.Tests.Browser.Screens.PeopleRegistrationTests;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The e-mail address of another person, which the technical administration sees and corrects after the registration (issue 635, part 3, decision 11 of the owner), in a real Chromium over the
/// host with the real gates, routes and registry: the list leads to the page of each active person and of no deactivated one; the correction and the removal are kept, said after the saving and
/// seen by the person on their own account; the chain records the act and the address is only in the table of the addresses, never in the log, the address of a page, a header or the text;
/// a text that is no address is refused at the field; nobody but the administration reaches the page; and the page has no violation of WCAG 2.1 A and AA in the light and in the dark, fits a
/// phone and is used by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class PeopleEmailCorrectionTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private const string Own = "/minha-conta/email";
    private const string Field = "#pessoa-email";
    private const string Refusal = "Escreva um endereço só, como nome@exemplo.org";
    private const int MostStops = 8;
    private const string FocusTheTitle = "() => { const title = document.querySelector('main h1'); title.setAttribute('tabindex', '-1'); title.focus(); }";
    private const string StopInMain = "() => { const stop = document.activeElement; return stop && stop.closest('main') ? (stop.id || stop.textContent.trim()) : ''; }";
    private const string TextOfMain = "() => document.querySelector('main').innerText";
    private const string Send =
        "async ([path, method, name, typed]) => { const init = { method, credentials: 'same-origin', redirect: 'manual' }; "
        + "if (method === 'POST') { init.body = new URLSearchParams({ [name]: typed }); } "
        + "const answer = await fetch(path, init); return [String(answer.status), await answer.text()]; }";

    [Fact]
    public async Task TheAdministrationCorrectsTheAddressOfAnotherPersonAndThePersonSeesTheNewOneOnTheAccount()
    {
        var (name, first, second) = (Made("corrigida"), Address("antigo"), Address("novo"));
        await using var admin = await RegisteredAsync(name, first);
        await Expect(admin.Page.Locator(Field)).ToHaveValueAsync(first);

        var response = await CorrectAsync(admin.Page, second);

        Assert.Equal(303, response.Status);
        Assert.Matches(new Regex(@"^/pessoas/\d+/email\?guardado$", RegexOptions.None, TimeSpan.FromSeconds(1)), response.Headers["location"]);
        await Expect(admin.Page.Locator(".notice--success")).ToContainTextAsync($"Endereço de e-mail de {name} corrigido.");
        await Expect(admin.Page.Locator(Field)).ToHaveValueAsync(second);
        await using var own = await OpenAsync(browser, people.Host, name, Own);
        await Expect(own.Page.Locator("#conta-email")).ToHaveValueAsync(second);
    }

    [Fact]
    public async Task AnEmptyFieldLeavesThePersonWithoutAddressAndThePageSaysSo()
    {
        var name = Made("sem-endereco");
        await using var admin = await RegisteredAsync(name, Address("apagado"));

        _ = await CorrectAsync(admin.Page, string.Empty);

        await Expect(admin.Page.Locator(".notice--success")).ToContainTextAsync($"{name} está sem endereço de e-mail.");
        await Expect(admin.Page.Locator(Field)).ToHaveValueAsync(string.Empty);
        await using var own = await OpenAsync(browser, people.Host, name, Own);
        await Expect(own.Page.Locator("#conta-email")).ToHaveValueAsync(string.Empty);
    }

    [Fact]
    public async Task TheChainRecordsTheCorrectionAndTheAddressIsOnlyInTheTableOfTheAddresses()
    {
        var address = Address("corrigido-log");
        await using var admin = await RegisteredAsync(Made("cadeia"), Address("antes-log"));

        var response = await CorrectAsync(admin.Page, address);

        var headers = string.Join('\n', (await response.AllHeadersAsync()).Select(header => $"{header.Key}: {header.Value}"));
        var seen = string.Join('\n', admin.Page.Url, await admin.Page.TitleAsync(), headers, await admin.Page.EvaluateAsync<string>(TextOfMain), people.Host.Log);
        Assert.DoesNotContain(address, seen, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(address), seen, StringComparison.Ordinal);
        var dump = await people.Host.DumpAsync();
        Assert.Contains("person_email_corrected", dump, StringComparison.Ordinal);
        var lines = dump.Split('\n').Where(line => line.Contains(address, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.StartsWith("INSERT INTO public.person_email ", line, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("dois@@exemplo.org")]
    [InlineData("Nome <nome@exemplo.org>")]
    public async Task ATextThatIsNoAddressIsRefusedAtTheFieldAndTheAddressKeptStays(string typed)
    {
        var kept = Address("mantido");
        await using var admin = await RegisteredAsync(Made("recusa"), kept);
        var page = admin.Page.Url;

        var response = await CorrectAsync(admin.Page, typed);

        Assert.Equal(400, response.Status);
        await Expect(admin.Page.Locator(".field__error")).ToContainTextAsync(Refusal);
        await Expect(admin.Page.Locator(Field)).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(admin.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        _ = await admin.Page.GotoAsync(page);
        await Expect(admin.Page.Locator(Field)).ToHaveValueAsync(kept);
    }

    [Fact]
    public async Task TheListLeadsToThePageOfEachActivePersonAndOfNoDeactivatedOne()
    {
        await using var admin = await OpenAsync(browser, people.Host, people.Host.Administrator, $"{List}?nome={PeopleScreenHost.Prefix}1");
        var shown = PeopleScreenHost.Listed.Where(seed => seed.Name.StartsWith($"{PeopleScreenHost.Prefix}1", StringComparison.Ordinal)).ToList();

        foreach (var seed in shown)
        {
            var link = admin.Page.GetByRole(AriaRole.Link, new() { Name = $"E-mail de {seed.Name}", Exact = true });
            await Expect(link).ToHaveCountAsync(seed.Active ? 1 : 0);
        }

        Assert.Contains(shown, seed => !seed.Active);
    }

    [Theory]
    [InlineData("curadoria")]
    [InlineData("auditoria")]
    [InlineData("anotacao")]
    public async Task NobodyButTheAdministrationReachesThePageOfTheAddressOfAnotherPerson(string who)
    {
        var address = Address("alheio");
        await using var admin = await RegisteredAsync(Made("alheia"), address);
        var person = who switch
        {
            "curadoria" => people.Host.Curator,
            "auditoria" => people.Host.Auditor,
            _ => people.Host.Annotator,
        };

        await using var other = await OpenAsync(browser, people.Host, person, admin.Page.Url);

        await Expect(other.Page.Locator(Field)).ToHaveCountAsync(0);
        Assert.DoesNotContain(address, await other.Page.ContentAsync(), StringComparison.Ordinal);

        foreach (var method in new[] { "GET", "POST" })
        {
            var (refusedStatus, refusedBody) = await SendAsync(other.Page, method, new Uri(admin.Page.Url).AbsolutePath, "alheio-novo@exemplo.org");
            var (missingStatus, missingBody) = await SendAsync(other.Page, method, "/zqx-nao-existe-6351", "alheio-novo@exemplo.org");
            Assert.Equal(("404", missingBody), (refusedStatus, refusedBody));
            Assert.Equal("404", missingStatus);
        }

        _ = await admin.Page.ReloadAsync();
        await Expect(admin.Page.Locator(Field)).ToHaveValueAsync(address);
    }

    [Theory]
    [InlineData("GET", null)]
    [InlineData("POST", "novo@exemplo.org")]
    [InlineData("POST", "sem-arroba")]
    [InlineData("POST", "")]
    public async Task ThePageOfADeactivatedPersonSaysSoWithNoFieldAndTheSendingGets409AndKeepsTheAddress(string method, string? typed)
    {
        var kept = Address("desativada");
        await using var admin = await RegisteredAsync(Made("desativada"), kept);
        var page = new Uri(admin.Page.Url).AbsolutePath;
        await people.Host.DeactivateAsync(long.Parse(IdOfThePage().Match(page).Groups[1].Value, CultureInfo.InvariantCulture));

        var (status, body) = await SendAsync(admin.Page, method, page, typed);

        Assert.Equal(method == "GET" ? "200" : "409", status);
        Assert.DoesNotContain("id=\"pessoa-email\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"corrigir-email\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Refusal, body, StringComparison.Ordinal);
        var dump = await people.Host.DumpAsync();
        Assert.Contains(kept, dump, StringComparison.Ordinal);
        Assert.DoesNotContain("novo@exemplo.org", dump, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task ThePageHasNoViolationOfWcagAAndAaInTheLightAndInTheDarkAfterACorrectionAndARefusal(string name)
    {
        var mode = MediaMode.Named(name);
        await using var admin = await RegisteredAsync(Made("contraste"), Address("contraste"));
        _ = await CorrectAsync(admin.Page, Address("contraste-novo"));
        await mode.ApplyAsync(admin.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(admin.Page);
        await RealBackgroundContrast.AssertAaAsync(admin.Page);
        await ModeCapture.SaveAsync(admin.Page, mode, "pessoa-email-corrigido");

        _ = await CorrectAsync(admin.Page, "sem-arroba");
        await mode.ApplyAsync(admin.Page);
        await AccessibilityCheck.AssertNoViolationsAsync(admin.Page);
        await RealBackgroundContrast.AssertAaAsync(admin.Page);
    }

    [Fact]
    public async Task ThePageFitsThePhoneWithoutScrollingSideways()
    {
        await using var admin = await RegisteredAsync(Made("celular"), Address("celular"));
        await admin.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await admin.Page.ReloadAsync();

        var overflow = await admin.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"The page is {overflow} px wider than {PhoneWidth} px.");
        await AccessibilityCheck.AssertNoViolationsAsync(admin.Page);
    }

    [Fact]
    public async Task TheKeyboardAloneCorrectsTheAddressAndGoesBackToThePeople()
    {
        var address = Address("teclado");
        await using var admin = await RegisteredAsync(Made("teclado"), Address("teclado-antes"));

        Assert.Equal(["pessoa-email", "corrigir-email", "Voltar às pessoas"], await StopsInMainAsync(admin.Page));
        await FocusTheTitleAsync(admin.Page);
        await admin.Page.Keyboard.PressAsync("Tab");
        await admin.Page.Keyboard.PressAsync("Control+A");
        await admin.Page.Keyboard.TypeAsync(address);
        await admin.Page.Keyboard.PressAsync("Tab");
        await admin.Page.Keyboard.PressAsync("Enter");

        await Expect(admin.Page).ToHaveURLAsync(new Regex(@"/pessoas/\d+/email\?guardado$"));
        await Expect(admin.Page.Locator(Field)).ToHaveValueAsync(address);
        await admin.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar às pessoas" }).FocusAsync();
        await admin.Page.Keyboard.PressAsync("Enter");
        await Expect(admin.Page).ToHaveURLAsync(new Regex("/pessoas$"));
    }

    private static string Address(string kind) => $"{Made(kind)}@exemplo.org";

    private static async Task<IResponse> CorrectAsync(IPage page, string typed)
    {
        await page.Locator(Field).FillAsync(typed);
        return await page.RunAndWaitForResponseAsync(() => page.Locator("#corrigir-email").ClickAsync(), response => response.Request.Method == "POST");
    }

    /// <summary>The stops of the keyboard inside the content, from the title on, until the focus leaves it: the id of the stop or, without one, its text.</summary>
    private static async Task<List<string>> StopsInMainAsync(IPage page)
    {
        await FocusTheTitleAsync(page);
        var stops = new List<string>();
        var stop = "-";
        for (var press = 0; press < MostStops && stop.Length > 0; press++)
        {
            await page.Keyboard.PressAsync("Tab");
            stop = await page.EvaluateAsync<string>(StopInMain);
            stops.Add(stop);
        }

        return [.. stops.Where(reached => reached.Length > 0)];
    }

    private static async Task FocusTheTitleAsync(IPage page) => _ = await page.EvaluateAsync(FocusTheTitle);

    [GeneratedRegex(@"/pessoas/(\d+)/email", RegexOptions.None, 2000)]
    private static partial Regex IdOfThePage();

    /// <summary>Sends the request from the page itself, with its cookie and its origin, and reads what comes back without following a redirect: the status as text and the body.</summary>
    private static async Task<(string Status, string Body)> SendAsync(IPage page, string method, string path, string? typed)
    {
        var sent = await page.EvaluateAsync<string[]>(Send, new object?[] { path, method, AccountEmailScreen.EmailField, typed });
        return (sent[0], sent[1]);
    }

    /// <summary>The administration registers a person with the address given and, from the list it lands on, opens the page of the address of that person.</summary>
    private async Task<BrowserTab> RegisteredAsync(string name, string address)
    {
        var admin = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        try
        {
            await FillAsync(admin.Page, name, "Anotação", people.Host.Password);
            await admin.Page.Locator(Field).FillAsync(address);
            Assert.Equal(303, (await SubmitAsync(admin.Page)).Status);
            await admin.Page.GetByRole(AriaRole.Link, new() { Name = $"E-mail de {name}", Exact = true }).ClickAsync();
            await Expect(admin.Page.GetByRole(AriaRole.Heading, new() { Name = $"E-mail de {name}", Level = 1 })).ToBeVisibleAsync();
            return admin;
        }
        catch
        {
            await admin.DisposeAsync();
            throw;
        }
    }
}
