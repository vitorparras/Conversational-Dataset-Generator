using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Fixtures;

[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SampleLogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Item visto")]
    public static partial void ItemSeen(this ILogger logger, string itemId);

    // A body written by hand hides under the class-level suppression.

    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
