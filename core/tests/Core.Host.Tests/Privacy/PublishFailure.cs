namespace Core.Host.Tests.Privacy;

/// <summary>What a failed publish says, written so that the cause is in the first line, the only one the report of the run shows, and the end of its output follows.</summary>
internal static class PublishFailure
{
    private const int KeptLines = 200;
    private const int CauseLength = 600;

    /// <summary>The message: the exit code and the lines that tell an error (or the last line there is) in the first line, then the last lines of the output.</summary>
    public static string Describe(int exitCode, string output)
    {
        var lines = output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var errors = lines.Where(line => line.Contains(": error ", StringComparison.Ordinal) || line.StartsWith("error ", StringComparison.Ordinal)).Distinct().Take(3);
        var cause = string.Join(" | ", errors);
        cause = cause.Length > 0 ? cause : lines.LastOrDefault() ?? "the publish wrote nothing";
        var first = cause.Length > CauseLength ? cause[..CauseLength] : cause;
        return $"dotnet publish exited with {exitCode}: {first}{Environment.NewLine}{string.Join(Environment.NewLine, lines.TakeLast(KeptLines))}";
    }
}
