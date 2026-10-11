using System.Text.RegularExpressions;

namespace Core.Tests.Architecture;

/// <summary>
/// Issue 34 CA-3, on the source we write: no direct call to the logger, and no interpolated string
/// in a logging file. Only generated <c>[LoggerMessage]</c> methods may write a log line.
/// </summary>
/// <remarks>
/// <c>BannedSymbols.txt</c> alone does not close the route. It bans the <c>LoggerExtensions</c>
/// overloads that take a free-text template, but it cannot ban <c>ILogger.Log&lt;TState&gt;</c>:
/// every generated <c>[LoggerMessage]</c> method calls it, the banned-API analyzer reports generated
/// code too, and banning it breaks the build of Core.Platform (RS0030 in <c>Logging.g.cs</c>).
/// Meanwhile <c>ILogger.Log&lt;TState&gt;</c> with an interpolated state compiles without a warning
/// and reaches every destination unredacted. This scan closes it. Generated code is never on disk
/// under <c>core/src</c> except in <c>obj/</c>: the <c>[LoggerMessage]</c> generator writes to the
/// compiler's memory, and <c>BuildInfo.g.cs</c> goes to <c>obj/</c>. A file named <c>*.g.cs</c>
/// anywhere else was written by hand and is scanned like any other.
/// It is a text scan, not a compiler: a false positive is fixed by rewording the line, never by an
/// exception list.
/// </remarks>
internal static partial class LoggingSourceRules
{
    /// <summary>Every rule violation in the C# files under <paramref name="root"/>, as <c>path:line: reason</c>.</summary>
    public static IReadOnlyList<string> Violations(DirectoryInfo root)
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles(root))
        {
            var relative = Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/');
            var inLogging = relative.Split('/').Contains("Logging", StringComparer.Ordinal);
            var found = new List<(int Line, string Reason)>();

            // A comment may name the logger to explain the rule; it calls nothing. Blanked, not
            // removed, so the line numbers stay true.
            var lines = File.ReadAllLines(file.FullName)
                .Select(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line)
                .ToArray();

            // The whole file at once: "logger." on one line and "Log(" on the next is still a call.
            var text = string.Join('\n', lines);
            foreach (Match call in DirectLoggerCall().Matches(text))
            {
                var name = call.Groups["name"];
                found.Add((LineOf(text, name.Index), $"direct call to the logger ({name.Value}); write a [LoggerMessage] method"));
            }

            for (var index = 0; inLogging && index < lines.Length; index++)
            {
                if (InterpolatedString().IsMatch(lines[index]))
                {
                    found.Add((index + 1, "interpolated string in a logging file; log text comes only from [LoggerMessage] templates"));
                }
            }

            violations.AddRange(found.OrderBy(item => item.Line).Select(item => $"{relative}:{item.Line}: {item.Reason}"));
        }

        return violations;
    }

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>
    /// The C# files we write: everything but build output (<c>obj/</c>, <c>bin/</c>), where generated
    /// code lives. Never skipped by file name: a <c>*.g.cs</c> outside <c>obj/</c> is handwritten.
    /// </summary>
    private static IEnumerable<FileInfo> SourceFiles(DirectoryInfo root) =>
        root.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root.FullName, file.FullName)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"))
            .OrderBy(file => file.FullName, StringComparer.Ordinal);

    // ILogger.Log, ILogger.Log<T>, every LoggerExtensions shorthand and BeginScope, as a call or as a
    // method group, with any space or line break between the dot and the name. Math.Log is a logarithm.
    [GeneratedRegex(@"(?<!\bMathF?\s*)\.\s*(?<name>Log|LogTrace|LogDebug|LogInformation|LogWarning|LogError|LogCritical|BeginScope)\b")]
    private static partial Regex DirectLoggerCall();

    // $"...", $@"...", @$"..." and raw interpolated strings ($"""...""").
    [GeneratedRegex(@"\$+@?""|@\$+""")]
    private static partial Regex InterpolatedString();
}
