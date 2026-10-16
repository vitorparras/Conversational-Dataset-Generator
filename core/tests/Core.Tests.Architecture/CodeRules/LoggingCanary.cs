namespace Core.Tests.Architecture.CodeRules;

/// <summary>A class that calls every banned logging route, appended to a source file so that the build must refuse it there.</summary>
internal static class LoggingCanary
{
    private static readonly (string Route, string Marker)[] Calls =
    [
        ("LoggerExtensions", "LoggerExtensions.LogInformation("),
        ("ILogger.Log", "logger.Log("),
        ("LoggerMessage.Define", "LoggerMessage.Define("),
        ("ILogger.BeginScope", "logger.BeginScope("),
    ];

    public static IReadOnlyList<string> Routes => [.. Calls.Select(call => call.Route)];

    /// <summary>The source with the canary after it, and the line each route sits on.</summary>
    public static CanarySite AppendTo(string relativePath, string source, int ordinal)
    {
        var text = $"{source}{(source.EndsWith('\n') ? string.Empty : "\n")}\n{ClassText(ordinal)}";
        var lines = text.Split('\n');
        var start = source.Count(character => character == '\n');
        var routeLines = Calls.Select(call => Array.FindIndex(lines, start, line => line.Contains(call.Marker, StringComparison.Ordinal)) + 1);
        return new CanarySite(relativePath, text, [.. routeLines]);
    }

    private static string ClassText(int ordinal) => $$"""
        internal static class RoutesCanary{{ordinal}}
        {
            public static void Write(Microsoft.Extensions.Logging.ILogger logger, string text)
            {
                Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, text);
                logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, new Microsoft.Extensions.Logging.EventId(2), text, null, static (state, _) => state);
                Microsoft.Extensions.Logging.LoggerMessage.Define(Microsoft.Extensions.Logging.LogLevel.Error, new Microsoft.Extensions.Logging.EventId(3), text)(logger, null);
                _ = logger.BeginScope(text);
            }
        }

        """;
}
