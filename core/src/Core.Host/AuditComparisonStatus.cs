namespace Core.Host;

/// <summary>
/// The state of the last periodic comparison, as /status says it: the outcome, when it ended, how many differences of each kind it found, never what they are,
/// and the names of the log files the server's fall cut short, which are a notice and no difference and stay listed, once each, until the host restarts.
/// </summary>
internal sealed record AuditComparisonStatus(AuditComparisonResult Result, DateTimeOffset? At, int NewDifferences, int KnownDifferences, IReadOnlyList<string> CutOffFiles)
{
    internal static AuditComparisonStatus Pending { get; } = new(AuditComparisonResult.Pending, null, 0, 0, []);
}
