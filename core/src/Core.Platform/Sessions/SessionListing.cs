namespace Core.Platform.Sessions;

/// <summary>
/// One open session of a person as the page may show it: when it began, when it was last used and whether it is the one asking. The listing identifier names it for ending it and is
/// never the identifier of the session, which stays with the client that holds it; neither a network address nor the text of the browser is kept, only the short description of it.
/// </summary>
/// <param name="ListingId">A random number made for the session only to point at it in a list; it opens nothing.</param>
/// <param name="Start">When the person entered.</param>
/// <param name="LastActivity">When the session was last used.</param>
/// <param name="Current">True for the session that asked for the list.</param>
/// <param name="Device">The short description of the browser and system, such as "Firefox no Windows"; never a version or an address.</param>
public sealed record SessionListing(string ListingId, DateTimeOffset Start, DateTimeOffset LastActivity, bool Current, string Device);
