using System.Runtime.CompilerServices;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Settings;

/// <summary>
/// The register of the changes of configuration submitted on the screen (decision 0053): each submission is a row and the event of the chain about it, written together or not at all.
/// It never writes the file of the configuration and never applies a change: that is the applier's, on the server.
/// </summary>
public sealed class ConfigurationChanges
{
    /// <summary>The foreign key of the author, the only constraint of the table with this name: a person not registered.</summary>
    private const string AuthorConstraint = "configuration_change_author";

    private const string Insert =
        "INSERT INTO public.configuration_change (config_key, new_value, motive, submitted_by) VALUES (@key, @value, @motive, @author) RETURNING id";

    private const string Submitted = """
        SELECT c.config_key, c.new_value, c.motive, c.submitted_by, c.submitted_at, e.sequence,
               EXISTS (SELECT 1 FROM public.custody_event d
                       WHERE d.item_kind = 'configuration_change' AND d.item_id = c.id AND d.step IN ('configuration_approved', 'configuration_refused')) AS decided
        FROM public.configuration_change c
        JOIN public.custody_event e ON e.item_kind = 'configuration_change' AND e.item_id = c.id AND e.step = 'configuration_submitted'
        WHERE c.id = @id
        """;

    private const string Pending = """
        SELECT c.id, c.config_key, c.new_value, c.motive, c.submitted_by, c.submitted_at, e.sequence, false AS decided, p.name
        FROM public.configuration_change c
        JOIN public.custody_event e ON e.item_kind = 'configuration_change' AND e.item_id = c.id AND e.step = 'configuration_submitted'
        JOIN public.person p ON p.id = c.submitted_by
        WHERE c.submitted_at > @since AND NOT EXISTS (SELECT 1 FROM public.custody_event d
            WHERE d.item_kind = 'configuration_change' AND d.item_id = c.id AND d.step IN ('configuration_approved', 'configuration_refused'))
        ORDER BY c.submitted_at DESC, c.id DESC
        LIMIT 50
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The register over the database the application reaches.</summary>
    /// <param name="dataSource">The source of the connections with the application's credential.</param>
    public ConfigurationChanges(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Records the submission with its event in one transaction; a person not registered, deactivated or a chain that refuses leaves nothing written.</summary>
    /// <param name="submission">The key, the value, the motive and the author, in the shape the database takes.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<ConfigurationChangeResult> SubmitAsync(ConfigurationSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return submission.HasShape
            ? SubmittingAsync(submission, cancellationToken)
            : throw new ArgumentException("The submission has no shape the database takes: a key Section:Name, a value of printable ASCII and a motive in snake_case.", nameof(submission));
    }

    /// <summary>The submission with this identifier, with the event of its submission and whether it was decided; null when there is none.</summary>
    /// <param name="changeId">The submission asked for.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<SubmittedChange?> SubmittedAsync(long changeId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Submitted);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = changeId });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(changeId, name => reader[name]) : null;
            }
        }
    }

    /// <summary>The submissions still waiting for a decision, taken after the moment given, the latest first and at most fifty, each with the name of who submitted.</summary>
    /// <param name="since">The oldest moment a pending submission may have been taken at: before it, the submission expired.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<PendingChange>> PendingAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Pending);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since.UtcDateTime });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await AllAsync(reader.ReadAsync, name => reader[name], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The approved changes the core recorded as never to be applied, the latest first.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IReadOnlyList<RefusedChange>> NotAppliedAsync(CancellationToken cancellationToken) =>
        new ApprovedChanges(dataSource).RefusedAsync(cancellationToken);

    /// <summary>Records the decision on the submission, by a person other than the one who submitted, as the event that points at the submission; nothing else is written.</summary>
    /// <param name="deciderId">The person who decides.</param>
    /// <param name="change">The submission decided, as <see cref="SubmittedAsync" /> read it.</param>
    /// <param name="approved">Whether the change is approved; refused otherwise.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<CustodyResult> DecideAsync(long deciderId, SubmittedChange change, bool approved, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new CustodyChain(dataSource).AppendAsync(CustodyEvent.ConfigurationDecided(deciderId, change.Id, change.Sequence, approved), cancellationToken);
    }

    private static async Task<IReadOnlyList<PendingChange>> AllAsync(Func<CancellationToken, Task<bool>> next, Func<string, object> column, CancellationToken cancellationToken)
    {
        var pending = new List<PendingChange>();
        while (await next(cancellationToken).ConfigureAwait(false))
        {
            pending.Add(new PendingChange(Read((long)column("id"), column), (string)column("name")));
        }

        return pending.AsReadOnly();
    }

    private static SubmittedChange Read(long changeId, Func<string, object> column) => new()
    {
        Id = changeId,
        Key = (string)column("config_key"),
        Value = (string)column("new_value"),
        Motive = (string)column("motive"),
        AuthorId = (long)column("submitted_by"),
        SubmittedAt = new DateTimeOffset((DateTime)column("submitted_at")),
        Sequence = (long)column("sequence"),
        Decided = (bool)column("decided"),
    };

    private async Task<ConfigurationChangeResult> SubmittingAsync(ConfigurationSubmission submission, CancellationToken cancellationToken)
    {
        var change = new StrongBox<long>();
        var recorded = await CustodyTransaction.AppendAfterAsync(dataSource, scope => InsertAsync(scope, submission, change, cancellationToken), cancellationToken).ConfigureAwait(false)
            ?? CustodyRefusals.ActorUnknown();
        return recorded.Outcome == CustodyOutcome.Appended
            ? new ConfigurationChangeResult { Recorded = recorded, ChangeId = change.Value }
            : new ConfigurationChangeResult { Recorded = recorded };
    }

    private static async Task<CustodyEvent?> InsertAsync(CustodyScope scope, ConfigurationSubmission submission, StrongBox<long> change, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(Insert);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Text) { Value = submission.Key });
            _ = command.Parameters.Add(new NpgsqlParameter("value", NpgsqlDbType.Text) { Value = submission.Value });
            _ = command.Parameters.Add(new NpgsqlParameter("motive", NpgsqlDbType.Text) { Value = submission.Motive });
            _ = command.Parameters.Add(new NpgsqlParameter("author", NpgsqlDbType.Bigint) { Value = submission.AuthorId });
            try
            {
                change.Value = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            }
            catch (PostgresException refused) when (refused.ConstraintName == AuthorConstraint)
            {
                return null;
            }
        }

        return CustodyEvent.ConfigurationSubmitted(submission.AuthorId, change.Value);
    }
}
