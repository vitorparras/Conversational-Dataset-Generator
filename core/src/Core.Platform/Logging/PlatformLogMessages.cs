using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Logging;

/// <summary>Every message the platform writes to the log, each one generated from a fixed template.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
public static partial class PlatformLogMessages
{
    /// <summary>An item did not complete a step; the line carries the reference and the exception, never the item's content.</summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Falha ao processar o item")]
    public static partial void ItemFailed(
        this ILogger logger, [LogProperties(OmitReferenceName = true)] ItemReference item, Exception exception);
}
