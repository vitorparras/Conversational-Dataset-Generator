namespace Core.Tests.Architecture.CodeRules;

/// <summary>How a build ended: its exit code and everything it wrote, with path separators made uniform.</summary>
internal sealed record BuildOutcome(int ExitCode, string Output)
{
    public IReadOnlyList<string> Lines =>
        Output.Replace('\\', '/').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
