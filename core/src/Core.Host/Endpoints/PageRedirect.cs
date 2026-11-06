namespace Core.Host.Endpoints;

/// <summary>The answer that takes a browser to another page with a GET, whatever method it asked with: 303, never 302, so a form posted is not posted again.</summary>
internal static class PageRedirect
{
    internal static void To(HttpResponse response, string path)
    {
        response.StatusCode = StatusCodes.Status303SeeOther;
        response.Headers.Location = path;
    }

    /// <summary>The same, as the result of a route.</summary>
    internal static IResult Of(HttpContext context, string path)
    {
        To(context.Response, path);
        return Results.Empty;
    }
}
