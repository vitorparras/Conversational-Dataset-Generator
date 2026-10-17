namespace Core.Platform.Tests.Logging;

/// <summary>How an external process ended: its exit code and everything it wrote to both streams.</summary>
internal sealed record ProcessOutcome(int ExitCode, string Output)
{
    public IReadOnlyList<string> Lines => Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
