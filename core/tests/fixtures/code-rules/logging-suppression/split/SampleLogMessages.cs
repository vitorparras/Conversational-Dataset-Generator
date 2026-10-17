using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Fixtures.Split;

[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SampleLogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Item visto")]
    public static partial void ItemSeen(this ILogger logger, string itemId);
}
