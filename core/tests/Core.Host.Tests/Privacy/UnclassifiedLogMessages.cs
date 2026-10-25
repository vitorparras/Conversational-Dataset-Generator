using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Privacy;

/// <summary>A message that writes a field no one classified, the way the sweep's own proof needs it to.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class UnclassifiedLogMessages
{
    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Item sem classificação")]
    public static partial void TextSeen(this ILogger logger, [LogProperties(OmitReferenceName = true)] UnclassifiedItem item);
}
