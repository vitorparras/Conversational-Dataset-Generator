namespace Core.Host.Endpoints;

/// <summary>
/// One open session of the person who asks, as the list shows it: the identifier that points at it for ending it, never the one of the session,
/// and nothing of the network or the browser.
/// </summary>
/// <param name="Id">The identifier of the list.</param>
/// <param name="Start">When the person entered.</param>
/// <param name="LastActivity">When the session was last used.</param>
/// <param name="Current">True for the session that asked.</param>
internal sealed record AccountSessionShown(string Id, DateTimeOffset Start, DateTimeOffset LastActivity, bool Current);
