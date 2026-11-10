using Core.Host.Tests.Browser.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// What the tests of the screens of the emptying (issue 617) share: the routes, the words of the screens, the way into a screen as a person of the run and the reading of the facts of an item.
/// </summary>
internal static class EmptyingPages
{
    public const string Target = "[aria-labelledby=item-alvo-titulo]";
    public const string Entry = "/esvaziamento";
    public const string Confirm = "/esvaziamento/confirmar";
    public const string Missing = "/zqx-nao-existe-6170";
    public const string Overflow = "() => document.documentElement.scrollWidth - document.documentElement.clientWidth";
    public const string Focused = "() => document.activeElement.id || document.activeElement.value || document.activeElement.textContent.trim()";
    public const string FocusIsSeen =
        "() => { const s = getComputedStyle(document.activeElement); return document.activeElement.matches(':focus-visible') && s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }";
    public const int PhoneWidth = 390;
    public const int PhoneHeight = 844;
    public const int StatusNotFound = 404;
    public const string ByTheSubject = "pedido-de-remocao";
    public const string ByTheContract = "exigencia-do-contrato";
    public const string ByTheSubjectCaption = "Pedido de remoção da pessoa de quem o conteúdo fala";
    public const string ByTheContractCaption = "Exigência do contrato com quem entregou o conteúdo";
    public const string NotANumber = "O identificador é um número inteiro maior que zero.";
    public const string Absent = "Nenhum item do domínio tem esse identificador.";
    public const string NotEmptied = "Sem esvaziamento registrado";

    /// <summary>The address of the entry with the item looked for.</summary>
    public static string EntryOf(long item) => $"{Entry}?item={item}";

    /// <summary>The address of the confirmation of the item.</summary>
    public static string ConfirmOf(long item) => $"{Confirm}?item={item}";

    /// <summary>A protocol number of the kind the emptier accepts, made up for one test.</summary>
    public static string Protocol() => $"ZQX-{Guid.NewGuid():N}"[..20];

    /// <summary>A page, in a context of its own, of a person who entered by the form and opened the route given.</summary>
    public static async Task<BrowserTab> OpenAsync(BrowserFixture browser, ScreenHostBase host, string person, string route)
    {
        var tab = await ScreenSession.SignedInAsync(browser, host, person);
        try
        {
            _ = await tab.Page.GotoAsync(route);
            await Expect(tab.Page.Locator("h1")).ToBeVisibleAsync();
            return tab;
        }
        catch
        {
            await tab.DisposeAsync();
            throw;
        }
    }

    /// <summary>The facts the page tells about the item, each term with what it says.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> FactsAsync(IPage page)
    {
        var terms = await page.Locator(".facts__item dt").AllInnerTextsAsync();
        var values = await page.Locator(".facts__item dd").AllInnerTextsAsync();
        return terms.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second.Trim(), StringComparer.Ordinal);
    }

    /// <summary>Chooses the motive and types the protocol on the confirmation, without sending it.</summary>
    public static async Task FillAsync(IPage page, string motive, string protocol)
    {
        if (motive.Length > 0)
        {
            await page.Locator($"input[name=motivo][value={motive}]").CheckAsync();
        }

        await page.Locator("#protocolo").FillAsync(protocol);
    }

    /// <summary>Sends the confirmation and answers the response of the POST.</summary>
    public static Task<IResponse> SendAsync(IPage page) =>
        page.RunAndWaitForResponseAsync(() => page.Locator("#esvaziar").ClickAsync(), response => response.Request.Method == "POST");
}
