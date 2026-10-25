using Core.Platform.Configuration;
using Core.Platform.Logging;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The log file the configuration may name, beside the console: opened, checked and added to a logging pipeline.</summary>
internal static class LogFileDestination
{
    /// <summary>The file the configuration names, relative to the binary's directory; null when absent or when it cannot be opened.</summary>
    internal static FileLoggerProvider? Open(ConfigurationLoadResult loaded) =>
        loaded.Values.TryGetValue(HostConfiguration.LogFilePath, out var path)
            ? FileLoggerProvider.Open(
                Path.Combine(AppContext.BaseDirectory, path), new LineConsoleFormatter(TimeProvider.System), Console.Error)
            : null;

    /// <summary>A check on the log file only when the configuration names one; without it the log goes to the console alone.</summary>
    internal static IEnumerable<IStartupCheck> Checks(ConfigurationLoadResult loaded, ILoggerProvider? fileLog) =>
        loaded.Values.ContainsKey(HostConfiguration.LogFilePath)
            ? [new LogFileCheck(HostConfiguration.LogFilePath, opened: fileLog is not null)]
            : [];
}
