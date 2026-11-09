using Microsoft.AspNetCore.Http.Features;

namespace Core.Host.Endpoints;

/// <summary>
/// The size of the body the two routes that receive a seal accept: the text of a seal is a hundred and some characters, so a body above the limit is refused with 413 before it is read, by the
/// size it announces or, when it announces none, as soon as it reads past the limit.
/// </summary>
internal static class SealBodyLimit
{
    internal const long Bytes = 1024;

    internal static void Use(IApplicationBuilder app) =>
        _ = app.Use(async (context, next) =>
        {
            if (!HoldsASeal(context.Request))
            {
                await next(context).ConfigureAwait(false);
            }
            else if (context.Request.ContentLength > Bytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            }
            else
            {
                Limit(context);
                await next(context).ConfigureAwait(false);
            }
        });

    private static readonly string[] SealRoutes = [ChainVerificationEndpoint.Route, SealEndpoint.ConfirmationRoute, ScreenPaths.SealConfirm];

    private static bool HoldsASeal(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && SealRoutes.Contains(request.Path.Value, StringComparer.OrdinalIgnoreCase);

    private static void Limit(HttpContext context)
    {
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
        {
            feature.MaxRequestBodySize = Bytes;
        }
    }
}
