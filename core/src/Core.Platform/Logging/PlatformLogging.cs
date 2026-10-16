using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>How the platform logs: one line per event on the console, and every classified value erased before any destination sees it.</summary>
public static class PlatformLogging
{
    /// <summary>Replaces whatever providers the host added with the line console, and turns redaction on in the logging pipeline.
    /// The line console needs a <see cref="TimeProvider"/> registered.</summary>
    public static void Configure(ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        _ = logging.ClearProviders()
            .AddConsole(console => console.FormatterName = LineConsoleFormatter.FormatterName)
            .AddConsoleFormatter<LineConsoleFormatter, ConsoleFormatterOptions>()
            .EnableRedaction();
    }

    /// <summary>Erases material, identity and secret alike; no classification of the platform keeps its value.</summary>
    public static void EraseSensitiveData(IRedactionBuilder redaction)
    {
        ArgumentNullException.ThrowIfNull(redaction);
        _ = redaction.SetRedactor<ErasingRedactor>(
            DataClassifications.Material, DataClassifications.Identity, DataClassifications.Secret);
    }
}
