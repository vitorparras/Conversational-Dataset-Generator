using System.Data;
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
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = new NpgsqlCommand(Submitted, connection);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = changeId });
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(changeId, reader) : null;
                }
            }
        }
    }

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

    private static SubmittedChange Read(long changeId, IDataRecord reader) => new()
    {
        Id = changeId,
        Key = (string)reader["config_key"],
        Value = (string)reader["new_value"],
        Motive = (string)reader["motive"],
        AuthorId = (long)reader["submitted_by"],
        SubmittedAt = new DateTimeOffset((DateTime)reader["submitted_at"]),
        Sequence = (long)reader["sequence"],
        Decided = (bool)reader["decided"],
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
