using Microsoft.Extensions.Logging;

namespace Core.Platform.Logging;

/// <summary>
/// Messages any module may write about an item. Only generated <see cref="LoggerMessageAttribute"/>
/// methods write logs: the direct <c>ILogger.Log*</c> overloads are banned in <c>BannedSymbols.txt</c>.
/// </summary>
public static partial class PlatformLogMessages
{
    /// <summary>
    /// An item failed. The line names the item, the step and the reason, and the exception type
    /// only; the exception message and the item's content never reach a destination.
    /// </summary>
    public static void ItemFailed(this ILogger logger, ItemReference item, Exception exception) =>
        ItemFailed(logger, exception, item.ItemId, item.Step, item.Reason);

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Falha no item {ItemId}, passo {Step}: {Reason}")]
    private static partial void ItemFailed(ILogger logger, Exception exception, string itemId, string step, string reason);
}
