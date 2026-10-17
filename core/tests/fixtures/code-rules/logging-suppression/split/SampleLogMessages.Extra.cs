using Microsoft.Extensions.Logging;

namespace Fixtures.Split;

internal static partial class SampleLogMessages
{
    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
