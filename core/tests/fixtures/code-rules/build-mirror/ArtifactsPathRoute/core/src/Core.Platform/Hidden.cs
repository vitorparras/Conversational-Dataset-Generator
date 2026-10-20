#pragma warning disable RS0030
namespace Core.Platform;

internal static class Hidden
{
    public static void Write(Microsoft.Extensions.Logging.ILogger logger, string text) =>
        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, text);
}
