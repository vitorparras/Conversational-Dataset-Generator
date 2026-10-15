using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Logging;

/// <summary>A message a module might declare, carrying a classified record.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SampleLogMessages
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Item visto")]
    public static partial void ItemSeen(this ILogger logger, [LogProperties(OmitReferenceName = true)] SampleItem item);
}
