using System.Globalization;

namespace Core.Host.Endpoints;

/// <summary>
/// The notice that a person was just registered: a cookie the registration sets on its way to the list, which the list reads once and forgets. It never comes from the address, so no link
/// can make the page say that someone was registered; the <c>__Host-</c> prefix and the strict same site keep another origin from setting it. It holds the identifier the registry gave,
/// never the name, so that its size does not depend on what was typed; the list reads the name from the registry.
/// </summary>
internal static class RegisteredNotice
{
    internal const string Name = "__Host-cadastrada";

    private static readonly CookieOptions Attributes = new() { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict };

    internal static void Issue(HttpResponse response, long registered) => response.Cookies.Append(Name, registered.ToString(CultureInfo.InvariantCulture), Attributes);

    /// <summary>The identifier of the person the notice is about, which the response then forgets so that the notice is told once; null when there is no notice, or it holds no identifier.</summary>
    internal static long? Take(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var registered))
        {
            return null;
        }

        context.Response.Cookies.Delete(Name, Attributes);
        return long.TryParse(registered, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }
}
