using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using static Core.Host.Tests.Browser.Screens.PeoplePages;
using static Core.Host.Tests.Browser.Screens.PeopleRegistrationTests;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The form that registers a person and the list that follows it (issue 599) as the person at the screen meets them, in a real Chromium: no violation of WCAG 2.1 A and AA in the light, in the
/// dark, with less transparency and more contrast, in the state of a refusal and in the state of a registration done, the text reading over the glass, the width of a phone without scrolling
/// sideways, and the whole of it, from the name to the registration, reached and sent by the keyboard alone with the focus always seen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PeopleFormScreenTests(BrowserFixture browser, PeopleHostFixture people) : IClassFixture<PeopleHostFixture>
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("ReducedTransparency")]
    [InlineData("HighContrast")]
    public async Task TheFormHasNoViolationOfWcagAAndAaAndItsTextReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await mode.ApplyAsync(tab.Page);

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "pessoa-nova");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheFormAfterARefusalHasNoViolationOfWcagAAndAaAndTheErrorReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await mode.ApplyAsync(tab.Page);
        await FillAsync(tab.Page, people.Host.Auditor, "Avaliação", Made("senha"));
        _ = await SubmitAsync(tab.Page);
        await Expect(tab.Page.Locator("#pessoa-nome-erro")).ToBeVisibleAsync();

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "pessoa-nova-recusa");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheListAfterARegistrationHasNoViolationOfWcagAAndAaAndTheNoticeReadsOverTheGlass(string name)
    {
        var mode = MediaMode.Named(name);
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await mode.ApplyAsync(tab.Page);
        await FillAsync(tab.Page, Made("cadastrada"), "Auditoria", Made("senha"));
        await tab.Page.Locator("#cadastrar").ClickAsync();
        await Expect(tab.Page.Locator(".notice--success")).ToBeVisibleAsync();

        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, mode, "pessoa-cadastrada");
    }

    [Fact]
    public async Task TheFormAndItsRefusalFitThePhoneWidthWithoutScrollingSidewaysAndHaveNoViolation()
    {
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        await tab.Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        _ = await tab.Page.GotoAsync(Form);

        var empty = await tab.Page.EvaluateAsync<int>(Overflow);
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "pessoa-nova");
        await FillAsync(tab.Page, people.Host.Auditor, "Avaliação", Made("senha"));
        _ = await SubmitAsync(tab.Page);
        await Expect(tab.Page.Locator("#pessoa-nome-erro")).ToBeVisibleAsync();
        var refused = await tab.Page.EvaluateAsync<int>(Overflow);

        Assert.True(empty <= 0, $"The form is {empty} px wider than the phone.");
        Assert.True(refused <= 0, $"The form refused is {refused} px wider than the phone.");
        await AccessibilityCheck.AssertNoViolationsAsync(tab.Page);
        await RealBackgroundContrast.AssertAaAsync(tab.Page);
        await ModeCapture.SaveAsync(tab.Page, "celular", "pessoa-nova-recusa");
    }

    [Fact]
    public async Task TheKeyboardAloneFillsTheNameChoosesTheFunctionTypesThePasswordReachesTheButtonWithTheFocusSeenAndRegisters()
    {
        var name = Made("teclado");
        await using var tab = await OpenAsync(browser, people.Host, people.Host.Administrator, Form);
        var order = new List<string>();
        var seen = new List<bool>();

        await tab.Page.Keyboard.TypeAsync(name);
        for (var stop = 0; stop < 4; stop++)
        {
            await tab.Page.Keyboard.PressAsync("Tab");
            order.Add(await tab.Page.EvaluateAsync<string>(Focused));
            seen.Add(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
            await tab.Page.Keyboard.TypeAsync(stop switch { 0 => "Audit", 1 => people.Host.Password, _ => string.Empty });
        }

        Assert.Equal(["pessoa-funcao", "pessoa-senha", "cadastrar", "Voltar à lista"], order);
        Assert.DoesNotContain(false, seen);
        await tab.Page.Keyboard.PressAsync("Shift+Tab");
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page).ToHaveURLAsync(new Regex($"/pessoas\\?nome={name}$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Equal([[name, "Auditoria", "Ativa"]], await RowsAsync(tab.Page));
    }
}
