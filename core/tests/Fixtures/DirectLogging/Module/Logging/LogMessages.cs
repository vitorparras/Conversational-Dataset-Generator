// Text fixture, never compiled: the legitimate shape, which the scan must accept. Log text comes
// only from a [LoggerMessage] template, and the call site goes through the generated method.
using Microsoft.Extensions.Logging;

namespace DirectLogging.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Processing {ItemId}")]
    public static partial void Processing(this ILogger logger, string itemId);

    public static void Use(ILogger logger, string itemId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger
                .Processing(itemId);
        }
    }
}
