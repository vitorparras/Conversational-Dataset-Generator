// Text fixture, never compiled: written by hand under a generated-looking name. Only build output
// (obj/, bin/) is skipped, never a file by its name, so the scan must report the call below.
namespace DirectLogging;

internal static class Handmade
{
    public static void Write(Microsoft.Extensions.Logging.ILogger logger, string text) =>
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, default, $"processing {text}", null, static (s, _) => s);
}
