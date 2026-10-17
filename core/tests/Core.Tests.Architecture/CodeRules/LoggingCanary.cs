namespace Core.Tests.Architecture.CodeRules;

/// <summary>A class that calls every banned logging route, written as a source file so that the build must refuse it.</summary>
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

    /// <summary>The line of each route inside the text, in the order of <see cref="Routes"/>.</summary>
    public static IReadOnlyList<int> RouteLines()
    {
        var lines = Text(0).Split('\n');
        return [.. Calls.Select(call => Array.FindIndex(lines, line => line.Contains(call.Marker, StringComparison.Ordinal)) + 1)];
    }

    public static string Text(int ordinal) => $$"""
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
