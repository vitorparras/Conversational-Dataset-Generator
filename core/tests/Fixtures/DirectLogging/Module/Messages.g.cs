// Text fixture, never compiled: generated code calls the logger, and the scan must leave it alone.
namespace DirectLogging;

internal static class Messages
{
    public static void Processing(Microsoft.Extensions.Logging.ILogger logger) =>
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, default, "state", null, static (s, _) => s);
}
