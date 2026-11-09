using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Core.Host.Tests.Browser.Screens.PeopleRegistrationTests;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The e-mail address of a person (issue 635, part 2) in a real Chromium, over the host with the real gates, the real routes and the real registry: the field is optional in the registration and
/// in the own account; only the person and the administration that registers see it, and no other person sees it, not even by an address that names it; the address is not in the log of the host,
/// in the address of a page, in a header, in the text of a page, nor in a record of the database that is not the one of the addresses, neither after a saving nor after a refusal; and the pages have
/// no violation of WCAG 2.1 A and AA in the light and in the dark, fit a phone and are reached by the keyboard alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeopleEmailTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    private const string Own = "/minha-conta/email";
    private const string Saved = "Endereço de e-mail guardado.";
    private const string WithoutAddress = "Você está sem endereço de e-mail.";
    private const string Refusal = "Escreva um endereço só, como nome@exemplo.org";
    private const string OwnField = "#conta-email";
    private const string FormField = "#pessoa-email";
    private const int StatusRefused = 400;
    private const int MostStops = 12;
    private const string FocusTheTitle = "() => { const title = document.querySelector('main h1'); title.setAttribute('tabindex', '-1'); title.focus(); }";
    private const string StopInMain = "() => { const stop = document.activeElement; return stop && stop.closest('main') ? (stop.id || stop.textContent.trim()) : ''; }";
    private const string TextOfMain = "() => document.querySelector('main').innerText";

    [Fact]
    public async Task TheFieldIsOptionalInTheRegistrationAndNothingIsAskedOfAPersonWithoutAnAddress()
    {
        var name = Made("sem-email");
        await using var admin = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        var field = admin.Page.Locator(FormField);
        await Expect(field).ToHaveAttributeAsync("type", "email");
        await Expect(field).Not.ToHaveAttributeAsync("aria-required", "true");
        await Expect(admin.Page.Locator("#pessoa-email[required]")).ToHaveCountAsync(0);
        await Expect(admin.Page.Locator("label[for=pessoa-email]")).ToContainTextAsync("(opcional)");
        await Expect(admin.Page.Locator("label[for=pessoa-email]")).Not.ToContainTextAsync("(obrigatório)");

        await FillAsync(admin.Page, name, "Anotação", people.Host.Password);
        var response = await SubmitAsync(admin.Page);

        Assert.Equal(303, response.Status);
        await Expect(admin.Page.Locator(".notice--success")).ToContainTextAsync($"{name} foi cadastrada");
        await using var own = await ScreenSession.SignedInAsync(browser, people.Host, name);
        _ = await own.Page.GotoAsync(Own);
        await Expect(own.Page.Locator(OwnField)).ToHaveValueAsync(string.Empty);
        await Expect(own.Page.Locator("label[for=conta-email]")).ToContainTextAsync("(opcional)");
        await Expect(own.Page.Locator(".notice--success")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheAddressTypedInTheRegistrationIsKeptForThePersonAndTheListOfTheAdministrationDoesNotShowIt()
    {
        var (name, address) = (Made("com-email"), Address("cadastro"));
        await using var admin = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(admin.Page, name, "Anotação", people.Host.Password);
        await admin.Page.Locator(FormField).FillAsync($"  {address}  ");

        var response = await SubmitAsync(admin.Page);

        Assert.Equal(303, response.Status);
        await Expect(admin.Page.Locator(".notice--success")).ToBeVisibleAsync();
        Assert.DoesNotContain(address, await admin.Page.ContentAsync(), StringComparison.Ordinal);
        await using var own = await ScreenSession.SignedInAsync(browser, people.Host, name);
        _ = await own.Page.GotoAsync(Own);
        await Expect(own.Page.Locator(OwnField)).ToHaveValueAsync(address);
    }

    [Fact]
    public async Task TheOwnPageKeepsChangesAndRemovesTheAddressAndSaysSoAfterTheSaving()
    {
        var (first, second) = (Address("primeiro"), Address("segundo"));
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Auditor, Own);
        await Expect(tab.Page.Locator(OwnField)).ToHaveValueAsync(string.Empty);

        var response = await SaveAsync(tab.Page, first);

        Assert.Equal(303, response.Status);
        Assert.Equal($"{Own}?guardado", response.Headers["location"]);
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync(Saved);
        await Expect(tab.Page.Locator(OwnField)).ToHaveValueAsync(first);
        _ = await SaveAsync(tab.Page, second);
        _ = await tab.Page.ReloadAsync();
        await Expect(tab.Page.Locator(OwnField)).ToHaveValueAsync(second);

        _ = await SaveAsync(tab.Page, string.Empty);

        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync(WithoutAddress);
        await Expect(tab.Page.Locator(OwnField)).ToHaveValueAsync(string.Empty);
    }

    [Fact]
    public async Task NoOtherPersonSeesTheAddressNotEvenByAnAddressThatNamesIt()
    {
        var address = Address("alheio");
        await using (var owner = await OpenAsync(browser, people.Host, people.Host.Curator, Own))
        {
            _ = await SaveAsync(owner.Page, address);
        }

        foreach (var other in new[] { people.Host.Annotator, people.Host.Auditor, people.Host.Administrator })
        {
            await using var tab = await OpenAsync(browser, people.Host, other, Own);
            foreach (var route in new[] { Own, $"{Own}?pessoa={people.Host.Curator}", $"{Own}?nome={people.Host.Curator}&id=1&email={Uri.EscapeDataString(address)}", $"{Own}/{people.Host.Curator}", $"{Own}?guardado" })
            {
                _ = await tab.Page.GotoAsync(route);

                Assert.DoesNotContain(address, await tab.Page.ContentAsync(), StringComparison.Ordinal);
                if (route == Own || route.StartsWith($"{Own}?", StringComparison.Ordinal))
                {
                    await Expect(tab.Page.Locator(OwnField)).Not.ToHaveValueAsync(address);
                }
            }
        }

        await using var administration = await OpenAsync(browser, people.Host, people.Host.Administrator, List);
        Assert.DoesNotContain(address, await administration.Page.ContentAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachPersonKeepsItsOwnAddressAndTheSavingOfAnotherDoesNotReplaceIt()
    {
        var (mine, theirs) = (Address("minha"), Address("deles"));
        await using var one = await OpenAsync(browser, people.Host, people.Host.Curator, Own);
        await using var two = await OpenAsync(browser, people.Host, people.Host.Annotator, Own);

        _ = await SaveAsync(one.Page, mine);
        _ = await SaveAsync(two.Page, theirs);
        _ = await one.Page.GotoAsync(Own);

        await Expect(one.Page.Locator(OwnField)).ToHaveValueAsync(mine);
        await Expect(two.Page.Locator(OwnField)).ToHaveValueAsync(theirs);
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("com espaco@exemplo.org")]
    [InlineData("dois@@exemplo.org")]
    [InlineData("Nome <nome@exemplo.org>")]
    [InlineData("nome@")]
    public async Task AnAddressThatIsNotOneIsRefusedAtTheFieldAndTheAddressKeptBeforeStays(string typed)
    {
        var kept = Address("mantido");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Annotator, Own);
        _ = await SaveAsync(tab.Page, kept);

        var response = await SaveAsync(tab.Page, typed);

        Assert.Equal(StatusRefused, response.Status);
        await Expect(tab.Page.Locator("#conta-email-erro, .field__error")).ToContainTextAsync(Refusal);
        await Expect(tab.Page.Locator(OwnField)).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(tab.Page.Locator(".notice--success")).ToHaveCountAsync(0);
        _ = await tab.Page.GotoAsync(Own);
        await Expect(tab.Page.Locator(OwnField)).ToHaveValueAsync(kept);
    }

    [Fact]
    public async Task AnAddressOfMoreThan254CharactersIsRefusedAtTheField()
    {
        var typed = $"{new string('a', 250)}@exemplo.org";
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Annotator, Own);

        var response = await SaveAsync(tab.Page, typed);

        Assert.Equal(StatusRefused, response.Status);
        await Expect(tab.Page.Locator(".field__error")).ToContainTextAsync(Refusal);
    }

    [Fact]
    public async Task TheAddressSavedIsNotInTheLogTheAddressTheHeadersTheTextOfThePageNorInAnyRecordButTheOneOfTheAddresses()
    {
        var address = Address("segredo");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Curator, Own);

        var response = await SaveAsync(tab.Page, address);

        Assert.Equal(303, response.Status);
        await AssertOnlyInTheFieldAsync(tab.Page, response, address, allowedInRecords: true);
        await AssertOnlyInTheAddressRecordsAsync(address);
    }

    [Fact]
    public async Task TheAddressRefusedInTheOwnPageIsNotInTheLogTheAddressTheHeadersTheTextOfThePageNorInTheDatabase()
    {
        var address = $"{Address("recusado")}@dois";
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Curator, Own);

        var response = await SaveAsync(tab.Page, address);

        Assert.Equal(StatusRefused, response.Status);
        await AssertOnlyInTheFieldAsync(tab.Page, response, address, allowedInRecords: false);
        Assert.DoesNotContain(address, await people.Host.DumpAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAddressRefusedInTheRegistrationIsNotInTheLogTheAddressTheHeadersTheTextOfThePageNorInTheDatabaseAndNoPersonIsMade()
    {
        var (name, address) = (Made("recusada"), $"{Address("cadastro-recusado")}@dois");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, name, "Anotação", people.Host.Password);
        await tab.Page.Locator(FormField).FillAsync(address);

        var response = await SubmitAsync(tab.Page);

        Assert.Equal(StatusRefused, response.Status);
        await Expect(tab.Page.Locator(".field__error")).ToContainTextAsync(Refusal);
        await Expect(tab.Page.Locator("#pessoa-nome")).ToHaveValueAsync(name);
        await Expect(tab.Page.Locator("#pessoa-senha")).ToHaveValueAsync(string.Empty);
        await AssertOnlyInTheFieldAsync(tab.Page, response, address, allowedInRecords: false);
        var seen = await people.Host.DumpAsync();
        Assert.DoesNotContain(address, seen, StringComparison.Ordinal);
        Assert.DoesNotContain(name, seen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAddressTypedInTheRegistrationIsNotInTheLogTheAddressTheHeadersNorInAnyRecordButTheOneOfTheAddresses()
    {
        var (name, address) = (Made("cadastrada-log"), Address("cadastro-log"));
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await FillAsync(tab.Page, name, "Anotação", people.Host.Password);
        await tab.Page.Locator(FormField).FillAsync(address);

        var response = await SubmitAsync(tab.Page);

        Assert.Equal(303, response.Status);
        await AssertOnlyInTheFieldAsync(tab.Page, response, address, allowedInRecords: true);
        await AssertOnlyInTheAddressRecordsAsync(address);
    }

    [Theory]
    [InlineData("Light", Own)]
    [InlineData("Dark", Own)]
    [InlineData("Light", Form)]
    [InlineData("Dark", Form)]
    public async Task ThePagesOfTheAddressHaveNoViolationOfWcagAAndAaInTheLightAndInTheDark(string name, string route)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, route);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, route == Own ? "minha-conta-email" : "pessoa-nova-email");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheRefusalOfTheAddressHasNoViolationOfWcagAAndAaInTheLightAndInTheDark(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Own);
        _ = await SaveAsync(tab.Page, "sem-arroba");
        await mode.ApplyAsync(tab.Page);

        await Expect(tab.Page.Locator(".field__error")).ToBeVisibleAsync();
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "minha-conta-email-recusa");
    }

    [Theory]
    [InlineData(Own)]
    [InlineData(Form)]
    public async Task ThePagesOfTheAddressFitThePhoneWithoutScrollingSidewaysAndHaveNoViolation(string route)
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, route);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(route);

        var overflow = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(overflow <= 0, $"{route}: the page is {overflow} px wider than {PhoneWidth} px.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
    }

    [Fact]
    public async Task TheKeyboardAloneWritesTheAddressSavesItAndGoesBackToTheAccount()
    {
        var address = Address("teclado");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Annotator, Own);

        Assert.Equal(["conta-email", "guardar-email", "Voltar à conta"], await StopsInMainAsync(tab.Page));
        await FocusTheTitleAsync(tab.Page);
        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Control+A");
        await tab.Page.Keyboard.TypeAsync(address);
        await tab.Page.Keyboard.PressAsync("Tab");
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Own)}\\?guardado$"));
        await Expect(tab.Page.Locator(".notice--success")).ToContainTextAsync(Saved);
        await Expect(tab.Page.Locator(OwnField)).ToHaveValueAsync(address);
        await tab.Page.GetByRole(AriaRole.Link, new() { Name = "Voltar à conta" }).FocusAsync();
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex("/minha-conta$"));
    }

    [Fact]
    public async Task TheKeyboardAloneReachesTheAddressOfTheAccountFromTheLinkOfThePanelAndTheFieldOfTheRegistrationInOrder()
    {
        await using var account = await OpenAsync(browser, people.Host, people.Host.Annotator, "/minha-conta");
        var link = account.Page.GetByRole(AriaRole.Link, new() { Name = "Ver ou mudar o e-mail" });
        await Expect(link).ToHaveAttributeAsync("href", Own);
        await link.FocusAsync();
        await account.Page.Keyboard.PressAsync("Enter");
        await Expect(account.Page.GetByRole(AriaRole.Heading, new() { Name = "Meu e-mail", Level = 1 })).ToBeVisibleAsync();

        await using var form = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);

        Assert.Equal(["pessoa-nome", "pessoa-funcao", "pessoa-senha", "pessoa-email", "cadastrar", "Voltar à lista"], await StopsInMainAsync(form.Page));
    }

    private static string Address(string kind) => $"{Made(kind)}@exemplo.org";

    private static async Task<IResponse> SaveAsync(IPage page, string typed)
    {
        _ = await page.GotoAsync(Own);
        await page.Locator(OwnField).FillAsync(typed);
        return await page.RunAndWaitForResponseAsync(() => page.Locator("#guardar-email").ClickAsync(), response => response.Request.Method == "POST");
    }

    /// <summary>The stops of the keyboard inside the content, from the title on, until the focus leaves it: the id of the stop or, without one, its text.</summary>
    private static async Task<List<string>> StopsInMainAsync(IPage page)
    {
        await FocusTheTitleAsync(page);
        var stops = new List<string>();
        for (var press = 0; press < MostStops; press++)
        {
            await page.Keyboard.PressAsync("Tab");
            var stop = await page.EvaluateAsync<string>(StopInMain);
            if (stop.Length == 0)
            {
                break;
            }

            stops.Add(stop);
        }

        return stops;
    }

    private static async Task FocusTheTitleAsync(IPage page) => _ = await page.EvaluateAsync(FocusTheTitle);

    /// <summary>
    /// What the person and the administration may see of the address is the field that holds it: it is not in the address of the page, in a header, in the title, in the text of the content, nor
    /// in the log of the host. The page that gives the address back in the value of the field is the only place of the page that has it.
    /// </summary>
    private async Task AssertOnlyInTheFieldAsync(IPage page, IResponse response, string address, bool allowedInRecords)
    {
        var headers = string.Join('\n', (await response.AllHeadersAsync()).Select(header => $"{header.Key}: {header.Value}"));
        var seen = string.Join('\n', page.Url, await page.TitleAsync(), headers, await page.EvaluateAsync<string>(TextOfMain), people.Host.Log);

        Assert.DoesNotContain(address, seen, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(address), seen, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(Regex.Escape(address), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5)), seen);
        Assert.True(allowedInRecords || !(await people.Host.DumpAsync()).Contains(address, StringComparison.Ordinal), "A refused address reached the database.");
    }

    /// <summary>The database holds the address in the table of the addresses and nowhere else: not in the chain of custody, not in the people, not in the sessions, not in a notice.</summary>
    private async Task AssertOnlyInTheAddressRecordsAsync(string address)
    {
        var lines = (await people.Host.DumpAsync()).Split('\n').Where(line => line.Contains(address, StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.StartsWith("INSERT INTO public.person_email ", line, StringComparison.Ordinal));
    }
}
