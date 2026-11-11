using System.Data.Common;
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
          AND NOT EXISTS (SELECT 1 FROM public.configuration_change newer
                          JOIN public.custody_event b ON b.item_kind = 'configuration_change' AND b.item_id = newer.id AND b.step = 'configuration_approved'
                          WHERE newer.config_key = c.config_key AND b.sequence > a.sequence)
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

    /// <summary>The approved changes that are not yet in force, in the order they were approved; one that a later approval of the same key has superseded is not among them.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<ApprovedChange>> PendingAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Pending);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await AllAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
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

    private static async Task<IReadOnlyList<RefusedChange>> AllRefusedAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var found = new List<RefusedChange>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            found.Add(ReadRefused(name => reader[name]));
        }

        return found;
    }

    private static RefusedChange ReadRefused(Func<string, object> column) => new()
    {
        Id = (long)column("id"),
        Key = (string)column("config_key"),
        ReasonCode = (string)column("reason_code"),
        ApprovalSequence = (long)column("approval_sequence"),
    };

    private static async Task<IReadOnlyList<ApprovedChange>> AllAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var found = new List<ApprovedChange>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            found.Add(Read(name => reader[name]));
        }

        return found;
    }

    private static ApprovedChange Read(Func<string, object> column) => new()
    {
        Id = (long)column("id"),
        Key = (string)column("config_key"),
        Value = (string)column("new_value"),
        SubmitterId = (long)column("submitted_by"),
        SubmissionSequence = (long)column("submission_sequence"),
        ApproverId = (long)column("approver"),
        ApprovalSequence = (long)column("approval_sequence"),
    };
}
