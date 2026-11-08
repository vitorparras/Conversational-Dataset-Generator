using Core.Persistence.Content;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The register of the submissions as it is read: the batches of the domains a person may see, whatever their state, a batch rejected included, with the motive and with what the submission brought.
/// The database judges the domain of each read, so a batch of another domain is never returned, whatever the filter says.
/// </summary>
public sealed class BatchRegister
{
    private const int IdColumn = 0;
    private const int StateColumn = 1;
    private const int MotiveColumn = 2;
    private const int OpenedByColumn = 3;
    private const int OpenedAtColumn = 4;
    private const int SubmissionColumn = 5;
    private const int BytesColumn = 6;
    private const int DigestColumn = 7;
    private const int WarningColumn = 8;
    private const int OriginColumn = 9;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The register over the database.</summary>
    /// <param name="dataSource">The source of the connections, with the privileges of the application.</param>
    public BatchRegister(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The batches of the domains in the filter, newest first, cut in the page the filter asks for.</summary>
    /// <param name="filter">The domains, the state, the period and the page.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IReadOnlyList<BatchEntry>> ListAsync(BatchFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegative(filter.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(filter.Take);
        return ListingAsync(filter, cancellationToken);
    }

    private async Task<IReadOnlyList<BatchEntry>> ListingAsync(BatchFilter filter, CancellationToken cancellationToken)
    {
        var found = new List<BatchEntry>();
        foreach (var domain in filter.Domains.Distinct())
        {
            found.AddRange(await InDomainAsync(domain, scope => ListInAsync(scope, domain, filter, cancellationToken), cancellationToken).ConfigureAwait(false));
        }

        var page = found.OrderByDescending(entry => entry.SubmittedAt).ThenByDescending(entry => entry.Id).Skip(filter.Skip).Take(filter.Take).ToList();
        return page;
    }

    /// <summary>One batch, found in the first of the domains given that holds it; null when none does.</summary>
    /// <param name="domains">The domains that may be seen.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<BatchEntry?> ReadAsync(IReadOnlyCollection<int> domains, long batchId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return ReadingAsync(domains, batchId, cancellationToken);
    }

    private async Task<BatchEntry?> ReadingAsync(IReadOnlyCollection<int> domains, long batchId, CancellationToken cancellationToken)
    {
        foreach (var domain in domains.Distinct())
        {
            var found = await InDomainAsync(domain, scope => ReadInAsync(scope, domain, batchId, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (found.Count > 0)
            {
                return found[0];
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<BatchEntry>> InDomainAsync(int domain, Func<ContentTransaction, Task<IReadOnlyList<BatchEntry>>> read, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await read(transaction).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<BatchEntry>> ListInAsync(ContentTransaction scope, int domain, BatchFilter filter, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ListIngestionBatches);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("state", NpgsqlDbType.Text) { Value = filter.State is { } state ? state.ToString().ToLowerInvariant() : DBNull.Value });
            _ = command.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = filter.From is { } since ? since : DBNull.Value });
            _ = command.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = filter.Until is { } until ? until : DBNull.Value });
            _ = command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = filter.Skip + filter.Take });
            return await ReadAllAsync(command, domain, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<BatchEntry>> ReadInAsync(ContentTransaction scope, int domain, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ReadIngestionBatchEntry);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batchId });
            return await ReadAllAsync(command, domain, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<BatchEntry>> ReadAllAsync(NpgsqlCommand command, int domain, CancellationToken cancellationToken)
    {
        var entries = new List<BatchEntry>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                entries.Add(Entry(reader, domain));
            }
        }

        return entries;
    }

    private static BatchEntry Entry(NpgsqlDataReader reader, int domain) => new()
    {
        Id = reader.GetInt64(IdColumn),
        DomainId = domain,
        State = Enum.Parse<BatchState>(reader.GetString(StateColumn), ignoreCase: true),
        Reason = reader[MotiveColumn] as string,
        SubmittedBy = reader.GetInt64(OpenedByColumn),
        SubmittedAt = reader.GetFieldValue<DateTimeOffset>(OpenedAtColumn),
        Receipt = reader[SubmissionColumn] is string submission
            ? new SubmissionReceipt(submission, reader.GetInt64(BytesColumn), reader.GetString(DigestColumn), reader[WarningColumn] as string)
            : null,
        OriginState = reader[OriginColumn] as string,
    };
}
