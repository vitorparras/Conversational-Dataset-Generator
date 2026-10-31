namespace Core.Host.Endpoints;

/// <summary>The cookie that carries the identifier of the session: the <c>__Host-</c> prefix makes a browser take it only from a secure origin, for the whole host and with no domain.</summary>
internal static class SessionCookie
{
    internal const string Name = "__Host-sessao";

    private static readonly CookieOptions Attributes = new() { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict };

    internal static string? Read(HttpRequest request) => request.Cookies.TryGetValue(Name, out var identifier) ? identifier : null;

    internal static void Issue(HttpResponse response, string identifier) => response.Cookies.Append(Name, identifier, Attributes);

    internal static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Attributes);
}
