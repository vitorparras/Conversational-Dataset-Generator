using Microsoft.Extensions.Logging;

namespace Fixtures;

#pragma warning disable RS0030
internal static class Reporter
{
    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
#pragma warning restore RS0030
#pragma warning disable
internal static class Other
{
}
#pragma warning restore
