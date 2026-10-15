using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Fixtures;

[SuppressMessage("ApiDesign", "RS0030")]
internal static partial class SampleLogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Item visto")]
    public static partial void ItemSeen(this ILogger logger, string itemId);
}
