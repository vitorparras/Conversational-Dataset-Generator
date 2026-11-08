using System.Globalization;

namespace Core.Host.Endpoints;

/// <summary>
/// What the last act on the own account did: a cookie the act sets on its way back to the account, which the account reads once and forgets. It never comes from the address, so no
/// link can make the page say that a password was changed or sessions were ended; the <c>__Host-</c> prefix and the strict same site keep another origin from setting it.
/// </summary>
internal static class AccountNotice
{
    internal const string Name = "__Host-feito";

    private const char Separator = ':';

    private static readonly CookieOptions Attributes = new() { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict };

    internal static void Issue(HttpResponse response, string done, int ended) =>
        response.Cookies.Append(Name, $"{done}{Separator}{ended.ToString(CultureInfo.InvariantCulture)}", Attributes);

    /// <summary>
    /// The act and how many sessions it ended, which the response then forgets so that the notice is told once; null when there is none, or the count is not a whole number of zero or more.
    /// </summary>
    internal static (string Done, int Ended)? Take(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var notice))
        {
            return null;
        }

        context.Response.Cookies.Delete(Name, Attributes);
        var parts = notice.Split(Separator);
        return parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ended) ? (parts[0], ended) : null;
    }
}
