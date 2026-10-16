using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Fixtures;

internal static class Reporter
{
    [UnconditionalSuppressMessage("ApiDesign", "RS0030", Justification = "The compiler honours this one as well.")]
    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
