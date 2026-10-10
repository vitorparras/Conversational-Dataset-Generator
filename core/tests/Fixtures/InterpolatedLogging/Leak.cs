using Microsoft.Extensions.Logging;

namespace InterpolatedLogging;

/// <summary>The escape route the build must close: a value interpolated straight into the message.</summary>
public static class Leak
{
    /// <summary>Writes the text unredacted, if it compiles.</summary>
    public static void Write(ILogger logger, string text) => logger.LogInformation($"processing {text}");
}
