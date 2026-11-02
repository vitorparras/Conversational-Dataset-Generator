namespace Core.Host;

/// <summary>The state of the last periodic comparison, as /status says it: the outcome, when it ended and how many differences of each kind it found, never what they are.</summary>
internal sealed record AuditComparisonStatus(AuditComparisonResult Result, DateTimeOffset? At, int NewDifferences, int KnownDifferences)
{
    internal static AuditComparisonStatus Pending { get; } = new(AuditComparisonResult.Pending, null, 0, 0);
}
