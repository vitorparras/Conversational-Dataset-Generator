using Microsoft.Extensions.Logging;

namespace Fixtures;

internal static class Reporter
{
    [Quiet("ApiDesign", "RS0030", Justification = "The alias is declared in another file.")]
    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
