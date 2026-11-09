using Core.Persistence.Recovery;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>Whether the ingestion takes material in: open, or blocked by the code of what is missing (decision 0014).</summary>
/// <param name="Open">Whether the gate is open.</param>
/// <param name="Code">The code of what keeps it closed; null when it is open.</param>
internal sealed record SubmissionGateStatus(bool Open, string? Code)
{
    /// <summary>The gate as the database says it: open once the recovery was rehearsed and the rehearsal is not stale.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    internal static async Task<SubmissionGateStatus> OfAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var status = await RecoveryGate.StatusAsync(dataSource, cancellationToken).ConfigureAwait(false);
        var code = status.Missing == RecoveryGate.StaleWords ? "recovery_rehearsal_stale" : "recovery_not_rehearsed";
        return status.Tested ? new SubmissionGateStatus(true, null) : new SubmissionGateStatus(false, code);
    }
}
