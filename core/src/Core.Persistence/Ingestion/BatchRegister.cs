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
    private const int PreviousColumn = 10;
    private const int SkipCeiling = 10_000;
    private const int TakeCeiling = 1_000;

    /// <summary>The most batches a page may skip: the server reads that many for each domain granted before it cuts the page, so the cost has a ceiling.</summary>
    public static int MostSkipped => SkipCeiling;

    /// <summary>The most batches a page may take.</summary>
    public static int MostTaken => TakeCeiling;

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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(filter.Skip, MostSkipped);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(filter.Take);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(filter.Take, MostTaken);
        return ListingAsync(filter, cancellationToken);
    }

    private async Task<IReadOnlyList<BatchEntry>> ListingAsync(BatchFilter filter, CancellationToken cancellationToken)
    {
        var found = new List<BatchEntry>();
        foreach (var domain in filter.Domains.Distinct())
        {
            found.AddRange(await ListOneAsync(domain, filter, cancellationToken).ConfigureAwait(false));
        }

        var page = found.OrderByDescending(entry => entry.SubmittedAt).ThenByDescending(entry => entry.Id).Skip(filter.Skip).Take(filter.Take).ToList();
        return page;
    }

    /// <summary>One batch, found in the first of the domains given that holds it; null when none does.</summary>
    /// <param name="domains">The domains that may be seen.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<BatchEntry?> ReadAsync(IEnumerable<int> domains, long batchId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return ReadingAsync(domains, batchId, cancellationToken);
    }

    private async Task<BatchEntry?> ReadingAsync(IEnumerable<int> domains, long batchId, CancellationToken cancellationToken)
    {
        foreach (var domain in domains.Distinct())
        {
            var found = await ReadOneAsync(domain, batchId, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The first batch of the domain that accepted a file with the digest; null when none did, and then a file with it is not a repeat.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="digest">The SHA-256 of the file, in lowercase hexadecimal.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<long?> PreviousAsync(int domainId, string digest, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(digest);
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(ContentQueries.ReadPreviousBatch);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("digest", NpgsqlDbType.Text) { Value = digest });
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as long?;
            }
        }
    }

    /// <summary>Where an item came from, found in the first of the domains given that holds it: its batch, when the submission came and who sent it; null when none does.</summary>
    /// <param name="domains">The domains that may be seen.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<ItemProvenance?> ProvenanceAsync(IEnumerable<int> domains, long itemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
        foreach (var domain in domains.Distinct())
        {
            var found = await ProvenanceInAsync(domain, itemId, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private async Task<ItemProvenance?> ProvenanceInAsync(int domain, long itemId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(ContentQueries.ReadItemProvenance);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = itemId });
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                        ? new ItemProvenance(reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetInt64(2), reader[3] as string)
                        : null;
                }
            }
        }
    }

    private async Task<IReadOnlyList<BatchEntry>> ListOneAsync(int domain, BatchFilter filter, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ListInAsync(transaction, domain, filter, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<BatchEntry?> ReadOneAsync(int domain, long batchId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var found = await ReadInAsync(transaction, domain, batchId, cancellationToken).ConfigureAwait(false);
            return found.Count > 0 ? found[0] : null;
        }
    }

    private static async Task<IReadOnlyList<BatchEntry>> ListInAsync(ContentTransaction scope, int domain, BatchFilter filter, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ReadIngestionBatchPage);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("state", NpgsqlDbType.Text) { Value = filter.State is { } state ? NameOf(state) : DBNull.Value });
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

    private static string NameOf(BatchState state) => state switch
    {
        BatchState.Open => "open",
        BatchState.Accepted => "accepted",
        BatchState.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The state is not one a batch has."),
    };

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
        PreviousBatch = reader.IsDBNull(PreviousColumn) ? null : reader.GetInt64(PreviousColumn),
    };
}
