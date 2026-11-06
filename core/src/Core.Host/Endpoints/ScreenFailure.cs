using Core.Host.Screens;

namespace Core.Host.Endpoints;

/// <summary>
/// Stands outside every gate and every route and answers a request for a page whose handling failed with 500 on the base page (decision 0051), never with the
/// detail of the failure: its message and its stack may carry text of a request or of the database. The log names only the kind of failure, as the end of the
/// process does. The page keeps the header of the encrypted channel the transport set. A request that does not ask for a page, or whose answer has already begun,
/// keeps the failure as it was, and the server ends it with 500.
/// </summary>
internal static class ScreenFailure
{
    internal static void Use(IApplicationBuilder app) =>
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception failure) when (Unhandled(context, failure))
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ScreenFailure)).ScreenFailed(failure.GetType().FullName);
                var transport = context.Response.Headers.StrictTransportSecurity;
                context.Response.Clear();
                context.Response.Headers.StrictTransportSecurity = transport;
                await ScreenResult.Failure(context).ExecuteAsync(context).ConfigureAwait(false);
            }
        });

    /// <summary>
    /// A failure the page answers: one the process can still report, of a request for a page whose answer has not begun. A request the server refused as malformed
    /// keeps its own status, and one the browser gave up on is no failure of the page.
    /// </summary>
    private static bool Unhandled(HttpContext context, Exception failure) =>
        ServingFailure.CanReport(failure) && failure is not BadHttpRequestException && !context.RequestAborted.IsCancellationRequested
        && !context.Response.HasStarted && SessionGate.AsksForPage(context.Request);
}
