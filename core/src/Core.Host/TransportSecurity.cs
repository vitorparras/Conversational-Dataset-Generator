using Microsoft.AspNetCore.CookiePolicy;

namespace Core.Host;

/// <summary>
/// What the host asks of the browser once the channel is encrypted: a cookie that travels only on it and, as an assumption no document
/// requires yet, that the browser come back only on it. The session that will carry a cookie does not exist yet (#65 and #70).
/// </summary>
internal static class TransportSecurity
{
    /// <summary>A year in seconds, which is what a browser keeps the rule for.</summary>
    private const string HstsValue = "max-age=31536000";

    /// <summary>Every cookie the host issues is marked <c>Secure</c> and out of reach of scripts.</summary>
    internal static void CookiePolicy(CookiePolicyOptions options)
    {
        options.Secure = CookieSecurePolicy.Always;
        options.HttpOnly = HttpOnlyPolicy.Always;
    }

    /// <summary>The cookie policy and the header that tells the browser not to return in clear, before any route.</summary>
    internal static void Map(IApplicationBuilder app)
    {
        _ = app.UseCookiePolicy();
        _ = app.Use(async (context, next) =>
        {
            context.Response.Headers.StrictTransportSecurity = HstsValue;
            await next(context).ConfigureAwait(false);
        });
    }
}
