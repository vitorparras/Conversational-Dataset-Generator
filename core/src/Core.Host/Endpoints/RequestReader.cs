using Core.Platform.Sessions;

namespace Core.Host.Endpoints;

/// <summary>
/// The one place that reads what a request carries for a route of a screen: the form, which is nothing when the body is not a form instead of a failure, and the session
/// the cookie names. A route never reads the body or the store by hand, so that a request with another kind of body is answered by the route and never breaks it.
/// </summary>
internal static class RequestReader
{
    /// <summary>The form the request carries, read with the token of the request, or nothing when the body is not a form.</summary>
    internal static async Task<IFormCollection?> FormAsync(HttpContext context) =>
        CarriesForm(context.Request) ? await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false) : null;

    /// <summary>Whether the body of the request is a form; the only question about the body that is not a read.</summary>
    internal static bool CarriesForm(HttpRequest request) => request.HasFormContentType;

    /// <summary>The person the session of the cookie belongs to, which this use keeps alive, or nothing when there is no cookie or the store does not know it.</summary>
    internal static long? SessionOf(HttpContext context) =>
        context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request));
}
