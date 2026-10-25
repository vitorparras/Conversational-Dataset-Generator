using System.Text.RegularExpressions;

namespace Core.Host.Tests;

/// <summary>The platform's log lines in a text, whole lines only, with and without their timestamp.</summary>
internal static partial class LogLines
{
    private const int TimestampLength = 25;

    /// <summary>Every line the text ends, leaving out a last one a killed process may have cut short.</summary>
    public static IReadOnlyList<string> Complete(string text) => text.ReplaceLineEndings("\n").Split('\n')[..^1];

    /// <summary>The complete lines in the platform's format, each without the timestamp that two destinations stamp apart.</summary>
    public static ISet<string> Untimed(string text) =>
        Complete(text).Where(line => Format().IsMatch(line)).Select(line => line[TimestampLength..]).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \S+\[\d+\] ")]
    public static partial Regex Format();
}
