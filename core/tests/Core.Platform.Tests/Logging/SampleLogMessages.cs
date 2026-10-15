using System.Diagnostics.CodeAnalysis;
using Core.Platform.Logging;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Tests.Logging;

/// <summary>Messages a module might declare: one carrying a classified record, one interpolating a classified value.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SampleLogMessages
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Item visto")]
    public static partial void ItemSeen(this ILogger logger, [LogProperties(OmitReferenceName = true)] SampleItem item);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning, Message = "Texto {text}")]
    public static partial void TextShown(this ILogger logger, [Material] string text);
}
