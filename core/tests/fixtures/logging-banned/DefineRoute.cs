using Microsoft.Extensions.Logging;

namespace LoggingBanned;

/// <summary>The route a message string takes through a hand-built LoggerMessage delegate, past the generator.</summary>
internal static class DefineRoute
{
    internal static void Write(ILogger logger, string text) =>
        LoggerMessage.Define(LogLevel.Error, new EventId(3), $"failed with {text}")(logger, null);
}
