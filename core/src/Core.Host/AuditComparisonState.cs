namespace Core.Host;

/// <summary>
/// The state of the last periodic comparison, written by the comparison and read by /status; a status is replaced whole, so a reader never sees half of one.
/// The log files the fall cut short are the exception to "the last": their names are kept, once each and in the order seen, until the host restarts.
/// </summary>
internal sealed class AuditComparisonState
{
    private volatile AuditComparisonStatus current = AuditComparisonStatus.Pending;

    internal AuditComparisonStatus Current => current;

    /// <summary>The names of the cut-off files of a round that no round before saw, which the service says in the log before it publishes the round.</summary>
    internal IReadOnlyList<string> Unseen(AuditComparisonStatus status) => [.. status.CutOffFiles.Except(current.CutOffFiles)];

    /// <summary>Takes the status of a round as the current one, with the cut-off files of the rounds before added to its own, and returns the names that were not known yet.</summary>
    internal IReadOnlyList<string> Record(AuditComparisonStatus status)
    {
        var known = current.CutOffFiles;
        var unknown = Unseen(status);
        current = status with { CutOffFiles = [.. known, .. unknown] };
        return unknown;
    }
}
