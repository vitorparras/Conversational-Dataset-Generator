using Microsoft.Extensions.Logging;

namespace LoggingBanned;

/// <summary>The route a message string takes through a logging scope opened on the interface itself.</summary>
internal static class ScopeRoute
{
    public static IDisposable? Open(ILogger logger, string text) => logger.BeginScope(text);
}
