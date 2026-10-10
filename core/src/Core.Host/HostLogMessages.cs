namespace Core.Host;

/// <summary>What the host writes about itself.</summary>
internal static partial class HostLogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Núcleo ouvindo em {Addresses}")]
    public static partial void Listening(this ILogger logger, string addresses);
}
