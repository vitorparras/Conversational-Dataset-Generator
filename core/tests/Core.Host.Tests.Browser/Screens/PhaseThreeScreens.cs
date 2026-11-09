namespace Core.Host.Tests.Browser.Screens;

/// <summary>What the checks that cross every screen of phase 3 share: the routes, the two sizes of the window and what the page is asked in the browser.</summary>
internal static class PhaseThreeScreens
{
    public const int PhoneWidth = 390;
    public const int PhoneHeight = 844;
    public const int DesktopWidth = 1280;
    public const int DesktopHeight = 800;
    public const string Notices = "/avisos";

    /// <summary>The share of the height of the phone that the header may take.</summary>
    public const double HeaderShareOfPhone = 0.15;

    /// <summary>The routes with a header: every screen of phase 3 the main has, and an address that leads nowhere, which answers with the page of one.</summary>
    public static IReadOnlyList<string> WithHeader { get; } =
    [
        "/",
        "/minha-conta",
        "/minha-conta/senha",
        "/minha-conta/sessoes/encerrar-outras",
        "/pessoas",
        "/estado-do-sistema",
        "/configuracao",
        "/dominios",
        "/custodia",
        "/custodia/verificar",
        Notices,
        "/zqx-nao-existe-734",
    ];

    /// <summary>The routes whose page holds a table, which has rows in the host of the run.</summary>
    public static IReadOnlyList<string> WithTable { get; } = ["/minha-conta", "/estado-do-sistema", "/configuracao", "/dominios", "/custodia", Notices];

    /// <summary>The height of the header, and of the window, in pixels.</summary>
    public const string HeaderHeight = "() => document.querySelector('header').getBoundingClientRect().height";

    /// <summary>Where the focus is: its top and bottom, the bottom of the header, and whether it is inside the header; nothing when the focus left the page.</summary>
    public const string FocusAgainstHeader =
        "() => { const e = document.activeElement; if (e === document.body) { return null; } const h = document.querySelector('header').getBoundingClientRect();"
        + " const r = e.getBoundingClientRect(); return [r.top, r.bottom, h.bottom, e.closest('header') ? 1 : 0]; }";

    /// <summary>The name of whatever has the focus, for the message of a failure.</summary>
    public const string FocusedName = "() => (document.activeElement.id || document.activeElement.className || document.activeElement.tagName)";

    /// <summary>
    /// Every table of the page that cuts a column or scrolls sideways: the region that holds it wider than it shows, and each cell outside the region. The page itself must not be wider than
    /// the window either.
    /// </summary>
    public const string CutTables =
        "() => { const bad = []; const view = document.documentElement.clientWidth;"
        + " if (document.documentElement.scrollWidth > view) { bad.push('page is ' + (document.documentElement.scrollWidth - view) + ' px wider than the window'); }"
        + " document.querySelectorAll('main table').forEach(t => { const name = t.querySelector('caption')?.textContent ?? 'table'; const region = t.closest('.data-table') ?? t.parentElement;"
        + " const box = region.getBoundingClientRect(); const over = region.scrollWidth - region.clientWidth;"
        + " if (over > 0) { bad.push(name + ': scrolls sideways by ' + over + ' px'); }"
        + " const cut = [...t.querySelectorAll('th, td')].filter(c => { const r = c.getBoundingClientRect(); return r.width > 1 && (r.right > box.right + 1 || r.left < box.left - 1); });"
        + " if (cut.length > 0) { bad.push(name + ': ' + cut.length + ' cells outside the region, the first is ' + cut[0].textContent.trim()); } }); return bad; }";
}
