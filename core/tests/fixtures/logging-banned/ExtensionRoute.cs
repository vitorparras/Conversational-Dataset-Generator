using Microsoft.Extensions.Logging;

namespace LoggingBanned;

/// <summary>The route a message string takes through the extension methods.</summary>
internal static class ExtensionRoute
{
    internal static void Write(ILogger logger, string text) => logger.LogInformation($"{text}");
}
