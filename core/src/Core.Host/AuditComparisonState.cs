namespace Core.Host;

/// <summary>The state of the last periodic comparison, written by the comparison and read by /status; a status is replaced whole, so a reader never sees half of one.</summary>
internal sealed class AuditComparisonState
{
    private volatile AuditComparisonStatus current = AuditComparisonStatus.Pending;

    internal AuditComparisonStatus Current => current;

    internal void Record(AuditComparisonStatus status) => current = status;
}
