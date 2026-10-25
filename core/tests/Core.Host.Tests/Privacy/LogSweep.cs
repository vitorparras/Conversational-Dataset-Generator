using Core.Host.Tests.Supervision;

namespace Core.Host.Tests.Privacy;

/// <summary>The one sweep every privacy test uses: the streams, the log file and the files the run wrote, each reported by name with the line that holds the marker.</summary>
internal static class LogSweep
{
    public const string Stdout = "stdout";
    public const string Stderr = "stderr";
    public const string LogFile = "Host:LogFile";

    public static IReadOnlyList<LogLeak> Leaks(LogEvidence evidence)
    {
        var marker = evidence.Marker;
        return
        [
            .. LinesHolding(Stdout, evidence.Stdout, marker),
            .. LinesHolding(Stderr, evidence.Stderr, marker),
            .. LinesHolding(LogFile, evidence.LogFile is null ? string.Empty : ReadShared(evidence.LogFile), marker),
            .. FilesHolding(evidence),
        ];
    }

    /// <summary>The text of a file another process may still hold open for writing.</summary>
    public static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<LogLeak> FilesHolding(LogEvidence evidence) =>
        evidence.Since is not { } since
            ? []
            : SecretSweep.FilesHolding(evidence.Marker, since)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => !IsTheLogFile(path, evidence.LogFile))
                .Select(path => new LogLeak(path, LinesHolding(path, ReadShared(path), evidence.Marker).FirstOrDefault()?.Line ?? string.Empty));

    private static IEnumerable<LogLeak> LinesHolding(string destination, string text, string marker) =>
        text.ReplaceLineEndings("\n").Split('\n')
            .Where(line => line.Contains(marker, StringComparison.Ordinal))
            .Select(line => new LogLeak(destination, line));

    private static bool IsTheLogFile(string path, string? logFile) =>
        logFile is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(logFile), StringComparison.OrdinalIgnoreCase);
}
