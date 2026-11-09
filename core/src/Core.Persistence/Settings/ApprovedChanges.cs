using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;

namespace Core.Persistence.Settings;

/// <summary>
/// The changes of configuration approved and not yet in force (decision 0053): what the applier reads, with a credential that only reads, and what the core marks as in force when it starts with the
/// value the change asked for. Reading never writes; the one write here is the event of the system, and the core is who makes it.
/// </summary>
public sealed class ApprovedChanges
{
    private const string Pending = """
        SELECT c.id, c.config_key, c.new_value, c.submitted_by, s.sequence AS submission_sequence, a.actor_person_id AS approver, a.sequence AS approval_sequence
        FROM public.configuration_change c
        JOIN public.custody_event s ON s.item_kind = 'configuration_change' AND s.item_id = c.id AND s.step = 'configuration_submitted'
        JOIN public.custody_event a ON a.item_kind = 'configuration_change' AND a.item_id = c.id AND a.step = 'configuration_approved'
        WHERE NOT EXISTS (SELECT 1 FROM public.custody_event f
                          WHERE f.item_kind = 'configuration_change' AND f.item_id = c.id AND f.step IN ('configuration_in_force', 'configuration_not_applied'))
        ORDER BY a.sequence
        """;

    private const string Refused = """
        SELECT c.id, c.config_key, split_part(n.reference, '/', 1) AS reason_code, split_part(n.reference, '/', 2)::bigint AS approval_sequence
        FROM public.configuration_change c
        JOIN public.custody_event n ON n.item_kind = 'configuration_change' AND n.item_id = c.id AND n.step = 'configuration_not_applied'
        ORDER BY n.sequence DESC
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The register over the database the caller reaches.</summary>
    /// <param name="dataSource">The source of the connections: the applier's, which only reads, or the application's.</param>
    public ApprovedChanges(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The approved changes that are not yet in force, in the order they were approved.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<ApprovedChange>> PendingAsync(CancellationToken cancellationToken)
    {
        var found = new List<ApprovedChange>();
        var command = dataSource.CreateCommand(Pending);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    found.Add(Read(reader));
                }
            }
        }

        return found;
    }

    /// <summary>Records that the core started with the value of the change, as the event of the system that points at the approval.</summary>
    /// <param name="change">The change in force, as <see cref="PendingAsync" /> read it.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<CustodyResult> RecordInForceAsync(ApprovedChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new CustodyChain(dataSource).AppendAsync(CustodyEvent.ConfigurationInForce(change.Id, change.ApprovalSequence), cancellationToken);
    }

    /// <summary>Records that the change will never be applied, as the event of the system with the code of the reason and the sequence of the approval.</summary>
    /// <param name="change">The change refused, as <see cref="PendingAsync" /> read it.</param>
    /// <param name="reasonCode">The code of the reason, in lower case and underscores.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<CustodyResult> RecordNotAppliedAsync(ApprovedChange change, string reasonCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new CustodyChain(dataSource).AppendAsync(CustodyEvent.ConfigurationNotApplied(change.Id, reasonCode, change.ApprovalSequence), cancellationToken);
    }

    /// <summary>The changes judged never to be applied, the latest first: what the screen of the change shows as not applied, with the reason in code.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<RefusedChange>> RefusedAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Refused);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await AllRefusedAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<IReadOnlyList<RefusedChange>> AllRefusedAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var found = new List<RefusedChange>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            found.Add(ReadRefused(reader));
        }

        return found;
    }

    private static RefusedChange ReadRefused(NpgsqlDataReader reader) => new()
    {
        Id = (long)reader["id"],
        Key = (string)reader["config_key"],
        ReasonCode = (string)reader["reason_code"],
        ApprovalSequence = (long)reader["approval_sequence"],
    };

    private static ApprovedChange Read(NpgsqlDataReader reader) => new()
    {
        Id = (long)reader["id"],
        Key = (string)reader["config_key"],
        Value = (string)reader["new_value"],
        SubmitterId = (long)reader["submitted_by"],
        SubmissionSequence = (long)reader["submission_sequence"],
        ApproverId = (long)reader["approver"],
        ApprovalSequence = (long)reader["approval_sequence"],
    };
}
