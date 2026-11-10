using Core.Persistence.Recovery;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>Whether the ingestion takes material in, asked of the database alone: no key is read, so the answer does not depend on one.</summary>
internal sealed class IntakeDoor
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The door over the database.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    internal IntakeDoor(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Open, or blocked by the code of what is missing (decision 0014).</summary>
    /// <param name="cancellationToken">Cancels the question.</param>
    internal async Task<SubmissionGateStatus> StatusAsync(CancellationToken cancellationToken)
    {
        var status = await RecoveryGate.StatusAsync(dataSource, cancellationToken).ConfigureAwait(false);
        if (status.Tested)
        {
            return new SubmissionGateStatus(true, null);
        }

        return new SubmissionGateStatus(false, status.Missing == RecoveryGate.StaleWords ? "recovery_rehearsal_stale" : "recovery_not_rehearsed");
    }
}
