namespace Core.Tests.Architecture.CodeRules;

/// <summary>How a <c>dotnet</c> command ended: its exit code and what it wrote, with path separators made uniform in the lines.</summary>
internal sealed record CommandOutcome(int ExitCode, string Output, string Error)
{
    public IReadOnlyList<string> Lines =>
        (Output + Error).Replace((char)92, '/').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>What the command printed, or a failure that carries everything it wrote when it did not exit cleanly.</summary>
    public string OutputOfSuccess(string description) =>
        ExitCode == 0
            ? Output
            : throw new InvalidOperationException($"{description} exited with code {ExitCode}:{Environment.NewLine}{Output}{Error}");
}
