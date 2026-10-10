// Text fixture, never compiled: the source scan must report every call below (issue 34 CA-3).
using Microsoft.Extensions.Logging;

namespace DirectLogging;

public static class Worker
{
    public static void Run(ILogger logger, string text)
    {
        logger.Log(LogLevel.Information, new EventId(1), $"processing {text}", null, static (s, _) => s);
        logger.Log<string>(LogLevel.Information, new EventId(2), text, null, static (s, _) => s);
        logger.LogInformation("processing {Text}", text);
        LoggerExtensions.LogWarning(logger, "processing");
        using var scope = logger.BeginScope($"item {text}");
        // logger.LogError("a comment calls nothing");
        _ = Math.Log(2.0);
    }
}
