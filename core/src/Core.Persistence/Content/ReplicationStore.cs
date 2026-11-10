using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content;

/// <summary>
/// Keeps the digests and the marks of the replicated content in the database and reads them back. It writes into a batch that is open and reads what the batches accepted left, in the domain
/// declared: the database refuses a batch that is not open, a change of a row already written and a domain other than the declared one. Nothing here is a text or a sender.
/// </summary>
public sealed class ReplicationStore
{
    private const int TextColumn = 2;
    private const int SenderColumn = 3;
    private const int LengthColumn = 4;
    private const int SaidColumn = 5;
    private const int FirstItemColumn = 2;
    private const int FirstPositionColumn = 3;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The store writes and reads through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The data source of the application.</param>
    public ReplicationStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Writes the occurrences of the messages that came in with the batch and the marks they brought, together: either everything is written or nothing is.</summary>
    /// <param name="target">The batch, which must be open, with its domain.</param>
    /// <param name="occurrences">The messages of the batch, in the order the criterion settled them.</param>
    /// <param name="marks">The marks the batch brought, to its messages and to the ones kept before.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task RecordAsync(ReplicationTarget target, IReadOnlyList<StoredOccurrence> occurrences, IReadOnlyList<StoredMark> marks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(target);
        return RecordingAsync(target, occurrences, marks, cancellationToken);
    }

    private async Task RecordingAsync(ReplicationTarget target, IReadOnlyList<StoredOccurrence> occurrences, IReadOnlyList<StoredMark> marks, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await ContentTransaction.BeginAsync(dataSource, target.DomainId, cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await WriteOccurrencesAsync(transaction, target, occurrences, cancellationToken).ConfigureAwait(false);
                await WriteMarksAsync(transaction, target, marks, cancellationToken).ConfigureAwait(false);
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

    /// <summary>The occurrences of the domain that the batches accepted left and whose text is one of the digests given, in the order they were written: the first of a content is the first of these.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="textDigests">The digests of the texts that arrive: nothing else is read, so the cost follows the batch and not the domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IReadOnlyList<StoredOccurrence>> OccurrencesAsync(int domainId, IReadOnlyCollection<string> textDigests, CancellationToken cancellationToken) =>
        ReadAsync(
            new Reading(domainId, ContentQueries.ReadReplicationOccurrences, textDigests),
            reader => new StoredOccurrence(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(TextColumn),
                reader.GetString(SenderColumn),
                reader.GetInt32(LengthColumn),
                reader.GetFieldValue<DateTimeOffset>(SaidColumn)),
            cancellationToken);

    /// <summary>The marks of the domain that the batches accepted left, on messages whose text is one of the digests given, in the order they were written.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="textDigests">The digests of the texts that arrive.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IReadOnlyList<StoredMark>> MarksAsync(int domainId, IReadOnlyCollection<string> textDigests, CancellationToken cancellationToken) =>
        ReadAsync(
            new Reading(domainId, ContentQueries.ReadReplicationMarks, textDigests),
            reader => new StoredMark(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt64(FirstItemColumn), reader.GetInt32(FirstPositionColumn)),
            cancellationToken);

    private static async Task WriteOccurrencesAsync(ContentTransaction transaction, ReplicationTarget target, IReadOnlyList<StoredOccurrence> occurrences, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(ContentQueries.InsertReplicationOccurrences);
        await using (command.ConfigureAwait(false))
        {
            Common(command, target);
            ArrayOf(command, "items", NpgsqlDbType.Bigint, occurrences.Select(one => one.Item).ToArray());
            ArrayOf(command, "positions", NpgsqlDbType.Integer, occurrences.Select(one => one.Position).ToArray());
            ArrayOf(command, "texts", NpgsqlDbType.Text, occurrences.Select(one => one.TextDigest).ToArray());
            ArrayOf(command, "senders", NpgsqlDbType.Text, occurrences.Select(one => one.SenderDigest).ToArray());
            ArrayOf(command, "lengths", NpgsqlDbType.Integer, occurrences.Select(one => one.Length).ToArray());
            ArrayOf(command, "saids", NpgsqlDbType.TimestampTz, occurrences.Select(one => one.SaidAt.UtcDateTime).ToArray());
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteMarksAsync(ContentTransaction transaction, ReplicationTarget target, IReadOnlyList<StoredMark> marks, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(ContentQueries.InsertReplicationMarks);
        await using (command.ConfigureAwait(false))
        {
            Common(command, target);
            ArrayOf(command, "items", NpgsqlDbType.Bigint, marks.Select(one => one.Item).ToArray());
            ArrayOf(command, "positions", NpgsqlDbType.Integer, marks.Select(one => one.Position).ToArray());
            ArrayOf(command, "first_items", NpgsqlDbType.Bigint, marks.Select(one => one.FirstItem).ToArray());
            ArrayOf(command, "first_positions", NpgsqlDbType.Integer, marks.Select(one => one.FirstPosition).ToArray());
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Common(NpgsqlCommand command, ReplicationTarget target)
    {
        _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = target.DomainId });
        _ = command.Parameters.Add(new NpgsqlParameter("batch", NpgsqlDbType.Bigint) { Value = target.BatchId });
    }

    private static void ArrayOf(NpgsqlCommand command, string name, NpgsqlDbType element, Array values) =>
        _ = command.Parameters.Add(new NpgsqlParameter(name, (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)element)) { Value = values });

    private async Task<IReadOnlyList<T>> ReadAsync<T>(Reading reading, Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
    {
        var found = new List<T>();
        var transaction = await ContentTransaction.BeginAsync(dataSource, reading.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(reading.Statement);
            await using (command.ConfigureAwait(false))
            {
                ArrayOf(command, "digests", NpgsqlDbType.Text, reading.Digests.ToArray());
                await EachRowAsync(command, reader => found.Add(map(reader)), cancellationToken).ConfigureAwait(false);
            }
        }

        return found;
    }

    private static async Task EachRowAsync(NpgsqlCommand command, Action<NpgsqlDataReader> each, CancellationToken cancellationToken)
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

    private sealed record Reading(int DomainId, string Statement, IReadOnlyCollection<string> Digests);
}
