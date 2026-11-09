namespace Core.Host.Tests.Browser.Screens;

/// <summary>
/// The stops of the keyboard in the header of a page after the entry, in the order the keyboard reaches them: the one place every test of the keyboard reads them from, so that a
/// change to the header changes this list and not the test of each screen.
/// </summary>
internal static class HeaderStops
{
    /// <summary>How a stop is read: its identifier, or its class when it has none; the body when nothing holds the focus.</summary>
    public const string FocusedId = "() => document.activeElement === document.body ? 'body' : (document.activeElement.id || document.activeElement.className)";

    /// <summary>The stops as <see cref="FocusedId"/> reads them: the link that skips to the content, the name of the system, the link of the own account and the button that leaves.</summary>
    public static IReadOnlyList<string> Signed { get; } = ["skip-link", "header__name", "nav__link header__account", "sair"];

    /// <summary>How many times the keyboard goes forward to leave the header.</summary>
    public static int Count => Signed.Count;

    /// <summary>The stops of the header followed by the stops of the page, in order.</summary>
    public static string[] Then(params string[] page) => [.. Signed, .. page];
}
