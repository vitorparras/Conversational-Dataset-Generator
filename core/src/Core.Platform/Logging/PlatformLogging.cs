using Core.Platform.Configuration;
using Core.Platform.Time;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>
/// The log path every module receives: redaction switched on before any destination, an erasing
/// redactor for the three classifications, and the same line written to console and file.
/// </summary>
public static class PlatformLogging
{
    /// <summary>The log file; empty writes to the console only. A relative path is next to the executable.</summary>
    public const string FilePath = "Logging:FilePath";

    /// <summary>The least severe level written.</summary>
    public const string MinimumLevel = "Logging:MinimumLevel";

    /// <summary>The keys the log path declares.</summary>
    public static ConfigurationSection Section { get; } = new(
        "Logging",
        [
            ConfigurationKey.WithDefault("FilePath", string.Empty),
            ConfigurationKey.WithDefault(
                "MinimumLevel",
                nameof(LogLevel.Information),
                acceptedValues:
                [
                    nameof(LogLevel.Trace), nameof(LogLevel.Debug), nameof(LogLevel.Information),
                    nameof(LogLevel.Warning), nameof(LogLevel.Error), nameof(LogLevel.Critical),
                ]),
        ]);

    /// <summary>Registers the log path with the destinations the configuration declares.</summary>
    public static IServiceCollection AddPlatformLogging(
        this IServiceCollection services,
        EffectiveConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var filePath = configuration.Get(FilePath);
        var level = Enum.Parse<LogLevel>(configuration.Get(MinimumLevel));
        return services.AddPlatformLogging(
            level,
            filePath.Length == 0 ? null : Path.GetFullPath(filePath, AppContext.BaseDirectory));
    }

    /// <summary>Registers the log path writing to the console and, when given, to a file.</summary>
    public static IServiceCollection AddPlatformLogging(
        this IServiceCollection services,
        LogLevel minimumLevel,
        string? filePath)
    {
        services.TryAddSingleton<IClock, SystemClock>();

        // Every classification is erased; the fallback for anything classified otherwise erases too.
        _ = services.AddRedaction(redaction =>
        {
            _ = redaction.SetRedactor<ErasingRedactor>(
                [.. DataClassifications.All.Select(classification => new DataClassificationSet(classification))]);
            _ = redaction.SetFallbackRedactor<ErasingRedactor>();
        });

        return services.AddLogging(logging =>
        {
            _ = logging.ClearProviders();
            _ = logging.SetMinimumLevel(minimumLevel);

            // Request lines carry the path and query, where an identifier can travel: only warnings.
            _ = logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            _ = logging.EnableRedaction();
            _ = logging.AddConsole(console => console.FormatterName = LineConsoleFormatter.FormatterName);
            _ = logging.AddConsoleFormatter<LineConsoleFormatter, ConsoleFormatterOptions>();
            if (filePath is not null)
            {
                _ = logging.Services.AddSingleton<ILoggerProvider>(provider =>
                    new FileLoggerProvider(filePath, provider.GetRequiredService<IClock>()));
            }
        });
    }
}
