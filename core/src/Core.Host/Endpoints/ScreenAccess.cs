namespace Core.Host.Endpoints;

/// <summary>
/// What the route of a screen declares of who reaches it (policy 006, decision 0051): the functions, and the caption and the place of its entry in the menu.
/// Nothing is reached by omission: a page asked of a route behind the session that declares nothing is answered as a route that does not exist, so the pages
/// everyone who entered may open, the start and the exit, say so with <see cref="Everyone"/>, never by leaving the mark out.
/// </summary>
internal sealed class ScreenAccess
{
    private ScreenAccess(IEnumerable<string>? functions, string? caption, int place)
    {
        Functions = functions;
        Caption = caption;
        Place = place;
    }

    /// <summary>The mark of a page everyone who entered may open, whatever the function; it has no entry in the menu.</summary>
    internal static ScreenAccess Everyone { get; } = new(functions: null, caption: null, place: 0);

    /// <summary>The functions that reach the screen; none for a page of everyone.</summary>
    internal IEnumerable<string>? Functions { get; }

    /// <summary>The words of the entry in the menu; none for a page that has no entry.</summary>
    internal string? Caption { get; }

    /// <summary>Where the entry stands in the menu, which lists the entries by this place and never by the order the routes were mapped in.</summary>
    internal int Place { get; }

    /// <summary>A page of everyone who entered, whatever the function, with its entry in the menu at the place given.</summary>
    internal static ScreenAccess ListedForEveryone(string caption, int place) => new(functions: null, caption, place);

    /// <summary>The screen of the functions given, with its entry in the menu at the place given.</summary>
    internal static ScreenAccess Of(string caption, int place, params string[] functions) => new(functions, caption, place);

    /// <summary>A page of the functions given with no entry in the menu, such as the confirmation of an action that a screen of the menu leads to.</summary>
    internal static ScreenAccess Unlisted(params string[] functions) => new(functions, caption: null, place: 0);
}
