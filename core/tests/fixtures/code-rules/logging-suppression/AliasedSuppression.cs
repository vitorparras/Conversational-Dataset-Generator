using Microsoft.Extensions.Logging;
using Quiet = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

namespace Fixtures;

internal static class Reporter
{
    [Quiet("ApiDesign", "RS0030", Justification = "The compiler honours the alias as well.")]
    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
