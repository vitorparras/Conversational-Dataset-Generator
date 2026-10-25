using Core.Platform.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Core.Host.Tests.Privacy;

/// <summary>Which destination of the sweep reads what each logger provider writes; a provider with no entry here is a destination nobody sweeps.</summary>
internal static class LogDestinations
{
    private static readonly Dictionary<Type, string> Swept = new()
    {
        [typeof(ConsoleLoggerProvider)] = LogSweep.Stdout,
        [typeof(FileLoggerProvider)] = LogSweep.LogFile,
    };

    /// <summary>The names of the provider types that no destination of the sweep reads.</summary>
    public static IReadOnlyList<string> Unswept(IEnumerable<Type> providers) =>
        [.. providers.Where(type => !Swept.ContainsKey(type)).Select(type => type.FullName ?? type.Name)];
}
