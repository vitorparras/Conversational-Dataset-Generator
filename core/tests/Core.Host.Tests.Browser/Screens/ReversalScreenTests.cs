using System.Globalization;
using System.Text.RegularExpressions;
using Core.Host.Tests.Browser.Accessibility;
using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The screens of the reversal of a marker (issues 614 and 615) in a real Chromium, over the host with the real gates, the real routes and the desk the root registers: the request is filled and
/// sent by the keyboard alone, with its button in the first fold of a phone, another person of the curation opens it from the list and authorizes it by the keyboard, and whoever asked
/// confirms the revelation and reads the identity once, which going back does not show again (issues 105 and 616); every page has no
/// violation of WCAG 2.1 A and AA, in the light and in the dark, on 1280 px and on the width of a phone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ReversalScreenTests(BrowserFixture browser, ReversalHostFixture reversal) : IClassFixture<ReversalHostFixture>
{
    private const string Ask = "/reversao/pedir";
    private const string RevealWait = "Registrando a revelação. A identidade aparece em seguida, uma só vez.";
    private const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";

    [Fact]
    public async Task RequestFormIsFilledByKeyboardAndPassesTheAccessibilityCheck()
    {
        var (item, marker) = await reversal.Case.ItemAsync();
        await using var tab = await EmptyingPages.OpenAsync(browser, reversal.Host, reversal.Host.Curator, Ask);
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-pedido");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-pedido");
        await CaptureAsync(tab.Page, "reversao-pedido");
        _ = await tab.Page.ReloadAsync();

        Assert.Equal("item", await tab.Page.EvaluateAsync<string>("() => document.activeElement.id"));
        foreach (var typed in new[] { item.ToString(CultureInfo.InvariantCulture), marker, "30", "zqx-motivo-sintetico", "zqx-uso-sintetico" })
        {
            await tab.Page.Keyboard.TypeAsync(typed);
            await tab.Page.Keyboard.PressAsync("Tab");
        }

        Assert.Equal("enviar", await tab.Page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex("/reversao/meus-pedidos/[0-9]+\\?enviado$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("^Pedido [0-9]+ enviado$", RegexOptions.None, TimeSpan.FromSeconds(1)), Level = 1 }))
            .ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".status")).ToContainTextAsync("Aguarda autorização");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-enviado");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-enviado");
        await CaptureAsync(tab.Page, "reversao-enviado");

        _ = await tab.Page.GotoAsync("/reversao/meus-pedidos");
        await Expect(tab.Page.Locator(".rows a", new() { HasText = marker })).ToBeVisibleAsync();
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-meus-pedidos");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-meus-pedidos");
        await CaptureAsync(tab.Page, "reversao-meus-pedidos");
    }

    [Fact]
    public async Task SendButtonIsVisibleWithoutScrollingOnAPhone()
    {
        await using var tab = await EmptyingPages.OpenAsync(browser, reversal.Host, reversal.Host.Curator, Ask);
        await tab.Page.SetViewportSizeAsync(390, 844);
        _ = await tab.Page.ReloadAsync();

        var send = tab.Page.Locator("#enviar");
        await Expect(send).ToBeInViewportAsync(new() { Ratio = 1 });
        Assert.Equal(0, await tab.Page.EvaluateAsync<double>("() => window.scrollY"));
        var box = await send.BoundingBoxAsync() ?? throw new InvalidOperationException("The button of the request is not on the page.");
        Assert.True(box.Y + box.Height <= 844, $"The button ends at {box.Y + box.Height} px, past the 844 px of the first fold.");
    }

    [Fact]
    public async Task AuthorizationIsConfirmedByKeyboardAndPassesTheAccessibilityCheck()
    {
        var (asked, _, _) = await reversal.Case.AskedAsync();
        await using var tab = await EmptyingPages.OpenAsync(browser, reversal.Host, reversal.Case.Second, "/reversao/autorizar");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-pendentes");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-pendentes");
        await CaptureAsync(tab.Page, "reversao-pendentes");
        await tab.Page.SetViewportSizeAsync(1280, 800);

        await FocusAsync(tab.Page, $"(document.activeElement.getAttribute('href') || '').endsWith('/reversao/autorizar/{asked}')");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await tab.Page.Keyboard.PressAsync("Enter");
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Pedido {asked}", Exact = true, Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".facts__item", new() { HasText = "Por que precisa" }).Locator("dd")).ToHaveTextAsync("zqx-motivo-sintetico");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-decisao");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-decisao");
        await CaptureAsync(tab.Page, "reversao-decisao");
        await tab.Page.SetViewportSizeAsync(1280, 800);

        await FocusAsync(tab.Page, "document.activeElement.id === 'autorizar'");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page).ToHaveURLAsync(new Regex($"/reversao/autorizar/{asked}\\?autorizado$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Pedido {asked} autorizado", Level = 1 })).ToBeVisibleAsync();
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-autorizado");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-autorizado");
        await CaptureAsync(tab.Page, "reversao-autorizado");
    }

    [Fact]
    public async Task RevelationIsReadByKeyboardAndPassesTheAccessibilityCheck()
    {
        var (request, item, marker) = await reversal.Case.AuthorizedAsync();
        await using var tab = await EmptyingPages.OpenAsync(browser, reversal.Host, reversal.Host.Curator, $"/reversao/meus-pedidos/{request}");
        await FocusAsync(tab.Page, $"(document.activeElement.getAttribute('href') || '').endsWith('/reversao/meus-pedidos/{request}/revelar')");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Revelar {marker}?", Exact = true, Level = 1 })).ToBeVisibleAsync();
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-confirmar");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-confirmar");
        await CaptureAsync(tab.Page, "reversao-confirmar");
        await FocusAsync(tab.Page, "document.activeElement.id === 'revelar'");
        Assert.True(await tab.Page.EvaluateAsync<bool>(FocusIsSeen));
        await tab.Page.Keyboard.PressAsync("Enter");

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"{marker}, item {item}", Exact = true, Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.GetByText(ReversalCase.Original, new() { Exact = true })).ToBeVisibleAsync();
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-revelada");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-revelada");
        await CaptureAsync(tab.Page, "reversao-revelada");

        _ = await tab.Page.GoBackAsync();
        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"Pedido {request}", Exact = true, Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.Locator(".status")).ToContainTextAsync(new Regex("Revelado em [0-9]{2}/[0-9]{2}/[0-9]{4}, [0-9]{2}:[0-9]{2}", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.DoesNotContain(ReversalCase.Original, await tab.Page.ContentAsync(), StringComparison.Ordinal);
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Light, "reversao-revelado");
        await ScreenAccessibility.AssertAccessibleAsync(tab.Page, MediaMode.Dark, "reversao-revelado");
    }

    [Fact]
    public async Task SecondSendOfRevealIsNotSentAndTheIdentityShows()
    {
        var (request, item, marker) = await reversal.Case.AuthorizedAsync();
        var confirm = $"/reversao/meus-pedidos/{request}/revelar";
        await using var tab = await EmptyingPages.OpenAsync(browser, reversal.Host, reversal.Host.Curator, confirm);
        _ = await tab.Page.AddInitScriptAsync(EntryScreenTests.SubmitRecorder);
        _ = await tab.Page.GotoAsync(confirm);
        var (onTheWay, release, posts) = (new TaskCompletionSource(), new TaskCompletionSource(), 0);
        _ = await tab.Page.RouteAsync($"**{confirm}", async route =>
        {
            if (route.Request.Method == "POST")
            {
                _ = Interlocked.Increment(ref posts);
                _ = onTheWay.TrySetResult();
                await release.Task;
            }

            await route.ContinueAsync();
        });

        var first = tab.Page.Locator("#revelar").ClickAsync();
        await onTheWay.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await tab.Page.Locator("#revelar").ClickAsync();
        _ = await tab.Page.WaitForFunctionAsync("() => JSON.parse(sessionStorage.getItem('submits') || '[]').length === 2");
        release.SetResult();
        await first;

        await Expect(tab.Page.GetByRole(AriaRole.Heading, new() { Name = $"{marker}, item {item}", Exact = true, Level = 1 })).ToBeVisibleAsync();
        await Expect(tab.Page.GetByText(ReversalCase.Original, new() { Exact = true })).ToBeVisibleAsync();
        var submits = await tab.Page.EvaluateAsync<string[][]>("() => JSON.parse(sessionStorage.getItem('submits') || '[]')");
        Assert.Equal(1, Volatile.Read(ref posts));
        Assert.Equal(2, submits.Length);
        Assert.Equal(["false", "true", RevealWait], submits[0]);
        Assert.Equal(["true", "true", RevealWait], submits[1]);
    }

    /// <summary>The page in the light on the phone and on the common width of a notebook, kept when the run asks for captures; the width of the checks comes back after.</summary>
    private static async Task CaptureAsync(IPage page, string screen)
    {
        await MediaMode.Light.ApplyAsync(page);
        foreach (var (width, height) in new[] { (390, 844), (1366, 768) })
        {
            await page.SetViewportSizeAsync(width, height);
            await ModeCapture.SaveAsync(page, string.Create(CultureInfo.InvariantCulture, $"{width}x{height}"), screen);
        }

        await page.SetViewportSizeAsync(1280, 800);
    }

    /// <summary>Presses Tab until the condition holds of the element with the focus, past the stops of the header; fails with the stops it went through when it never does.</summary>
    private static async Task FocusAsync(IPage page, string condition)
    {
        var stops = new List<string>();
        for (var stop = 0; stop < HeaderStops.Count + 20; stop++)
        {
            await page.Keyboard.PressAsync("Tab");
            if (await page.EvaluateAsync<bool>($"() => {condition}"))
            {
                return;
            }

            stops.Add(await page.EvaluateAsync<string>("() => `${document.activeElement.tagName}#${document.activeElement.id}`"));
        }

        Assert.Fail($"The keyboard never reached what holds: {condition}. It went through {string.Join(", ", stops)}.");
    }
}
