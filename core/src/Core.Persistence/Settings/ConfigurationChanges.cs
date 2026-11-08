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
    private const string AuthorConstraint = "configuration_change_author";

    private const string Insert =
        "INSERT INTO public.configuration_change (config_key, new_value, motive, submitted_by) VALUES (@key, @value, @motive, @author) RETURNING id";

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
            catch (PostgresException refused) when (refused.SqlState == PostgresErrorCodes.ForeignKeyViolation && refused.ConstraintName == AuthorConstraint)
            {
                return null;
            }
        }

        return CustodyEvent.ConfigurationSubmitted(submission.AuthorId, change.Value);
    }
}
