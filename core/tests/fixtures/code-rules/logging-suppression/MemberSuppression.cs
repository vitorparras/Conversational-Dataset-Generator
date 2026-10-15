using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Fixtures;

internal static partial class SampleLogMessages
{
    public static string Marker => "x";

    [SuppressMessage("ApiDesign", "RS0030", Justification = "One method only.")]
    public static void Write(ILogger logger, string text) =>
        logger.Log(LogLevel.Information, new EventId(2), text, null, static (state, _) => state);
}
