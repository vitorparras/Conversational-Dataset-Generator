using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Replicated;

/// <summary>
/// Keeps the digests and the marks of the replicated content in the database and reads them back. It writes into a batch that is open and reads what the batches accepted left, in the domain
/// declared: the database refuses a batch that is not open, a change of a row already written and a domain other than the declared one. Nothing here is a text or a sender.
/// </summary>
public sealed class ReplicationStore
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The store writes and reads through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The data source of the application.</param>
    public ReplicationStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Writes the occurrences of the messages that came in with the batch and the marks they brought, together: either everything is written or nothing is.</summary>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch, which must be open.</param>
    /// <param name="occurrences">The messages of the batch, in the order the criterion settled them.</param>
    /// <param name="marks">The marks the batch brought, to its messages and to the ones kept before.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public async Task RecordAsync(int domainId, long batchId, IReadOnlyList<StoredOccurrence> occurrences, IReadOnlyList<StoredMark> marks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(marks);
        try
        {
            var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await WriteAsync(transaction, domainId, batchId, occurrences, marks, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (PostgresException refused) when (refused.SqlState == CustodyStatements.ChainBrokenSqlState)
        {
            throw new CustodyChainBrokenException();
        }
        catch (PostgresException refused) when (refused.SqlState == CustodyStatements.BatchSqlState)
        {
            throw new BatchNotOpenException();
        }
    }

    /// <summary>The occurrences of the domain that the batches accepted left, in the order they were written: the first of a content is the first of these.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<StoredOccurrence>> OccurrencesAsync(int domainId, CancellationToken cancellationToken)
    {
        var found = new List<StoredOccurrence>();
        await ReadAsync(
            domainId,
            ContentQueries.ReadReplicationOccurrences,
            reader => found.Add(new StoredOccurrence(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetFieldValue<DateTimeOffset>(5))),
            cancellationToken).ConfigureAwait(false);
        return found;
    }

    /// <summary>The marks of the domain that the batches accepted left, in the order they were written.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<StoredMark>> MarksAsync(int domainId, CancellationToken cancellationToken)
    {
        var found = new List<StoredMark>();
        await ReadAsync(
            domainId,
            ContentQueries.ReadReplicationMarks,
            reader => found.Add(new StoredMark(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt64(2), reader.GetInt32(3))),
            cancellationToken).ConfigureAwait(false);
        return found;
    }

    private static async Task WriteAsync(
        ContentTransaction transaction,
        int domainId,
        long batchId,
        IReadOnlyList<StoredOccurrence> occurrences,
        IReadOnlyList<StoredMark> marks,
        CancellationToken cancellationToken)
    {
        if (occurrences.Count > 0)
        {
            var command = transaction.CreateCommand(ContentQueries.InsertReplicationOccurrences);
            await using (command.ConfigureAwait(false))
            {
                Common(command, domainId, batchId);
                ArrayOf(command, "items", NpgsqlDbType.Bigint, occurrences.Select(one => one.Item).ToArray());
                ArrayOf(command, "positions", NpgsqlDbType.Integer, occurrences.Select(one => one.Position).ToArray());
                ArrayOf(command, "texts", NpgsqlDbType.Text, occurrences.Select(one => one.TextDigest).ToArray());
                ArrayOf(command, "senders", NpgsqlDbType.Text, occurrences.Select(one => one.SenderDigest).ToArray());
                ArrayOf(command, "lengths", NpgsqlDbType.Integer, occurrences.Select(one => one.Length).ToArray());
                ArrayOf(command, "saids", NpgsqlDbType.TimestampTz, occurrences.Select(one => one.SaidAt.UtcDateTime).ToArray());
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (marks.Count > 0)
        {
            var command = transaction.CreateCommand(ContentQueries.InsertReplicationMarks);
            await using (command.ConfigureAwait(false))
            {
                Common(command, domainId, batchId);
                ArrayOf(command, "items", NpgsqlDbType.Bigint, marks.Select(one => one.Item).ToArray());
                ArrayOf(command, "positions", NpgsqlDbType.Integer, marks.Select(one => one.Position).ToArray());
                ArrayOf(command, "first_items", NpgsqlDbType.Bigint, marks.Select(one => one.FirstItem).ToArray());
                ArrayOf(command, "first_positions", NpgsqlDbType.Integer, marks.Select(one => one.FirstPosition).ToArray());
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void Common(NpgsqlCommand command, int domainId, long batchId)
    {
        _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = domainId });
        _ = command.Parameters.Add(new NpgsqlParameter("batch", NpgsqlDbType.Bigint) { Value = batchId });
    }

    private static void ArrayOf(NpgsqlCommand command, string name, NpgsqlDbType element, System.Array values) =>
        _ = command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | element) { Value = values });

    private async Task ReadAsync(int domainId, string statement, Action<NpgsqlDataReader> each, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(statement);
            await using (command.ConfigureAwait(false))
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        each(reader);
                    }
                }
            }
        }
    }
}
