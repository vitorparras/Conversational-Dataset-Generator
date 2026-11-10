namespace Core.Host.Tests.Privacy;

/// <summary>What a failed publish says, written so that the cause is in the first line, the only one the report of the run shows, and the end of its output follows.</summary>
internal static class PublishFailure
{
    private const int KeptLines = 200;
    private const int CauseLength = 600;

    /// <summary>The message for what the publish wrote to its two streams; a stdout that ends without a line break does not run into the first line of the stderr.</summary>
    public static string Describe(int exitCode, string standardOutput, string standardError) =>
        Describe(exitCode, standardOutput + "\n" + standardError);

    /// <summary>The message: the exit code and the lines that tell an error (or the last line there is) in the first line, then the last lines of the output.</summary>
    public static string Describe(int exitCode, string output)
    {
        var lines = output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var errors = lines.Where(line => line.Contains(": error ", StringComparison.Ordinal) || line.StartsWith("error ", StringComparison.Ordinal)).Distinct().Take(3);
        var cause = string.Join(" | ", errors);
        cause = cause.Length > 0 ? cause : lines.LastOrDefault(line => !IsBuildSummary(line)) ?? lines.LastOrDefault() ?? "the publish wrote nothing";
        var first = cause.Length > CauseLength ? cause[..CauseLength] : cause;
        return $"dotnet publish exited with {exitCode}: {first}{Environment.NewLine}{string.Join(Environment.NewLine, lines.TakeLast(KeptLines))}";
    }

    /// <summary>The closing lines MSBuild always writes (<c>Build FAILED.</c>, <c>N Warning(s)</c>, <c>N Error(s)</c>, <c>Time Elapsed</c>), which say nothing about the cause.</summary>
    private static bool IsBuildSummary(string line) =>
        line.StartsWith("Build FAILED", StringComparison.Ordinal)
        || line.StartsWith("Time Elapsed", StringComparison.Ordinal)
        || line.EndsWith("Error(s)", StringComparison.Ordinal)
        || line.EndsWith("Warning(s)", StringComparison.Ordinal);
}
