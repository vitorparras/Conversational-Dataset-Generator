using Microsoft.Extensions.Logging;

namespace LoggingBanned;

/// <summary>The route a message string takes through the interface itself, past every extension method.</summary>
internal static class InterfaceRoute
{
    internal static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, exception: null, static (state, _) => state);
}
