using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// Writes the rows of a batch at its opening: the batch, with what the submission brought and the state of its origin, and, when the origin was declared, the origin sealed, in the same transaction,
/// so that a batch that says it declared an origin never lacks it.
/// </summary>
internal static class BatchInsertion
{
    internal static async Task<long> InsertAsync(CustodyScope scope, BatchOpening opening, CancellationToken cancellationToken)
    {
        var batch = await InsertBatchAsync(scope, opening, cancellationToken).ConfigureAwait(false);
        if (opening.Origin is { Content: { } content, Key: { } key })
        {
            await InsertOriginAsync(scope, opening.DomainId, (batch, content.Envelope, key), cancellationToken).ConfigureAwait(false);
        }

        return batch;
    }

    private static async Task<long> InsertBatchAsync(CustodyScope scope, BatchOpening opening, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.InsertIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = opening.DomainId });
            _ = command.Parameters.Add(new NpgsqlParameter("author", NpgsqlDbType.Bigint) { Value = opening.AuthorId });
            _ = command.Parameters.Add(new NpgsqlParameter("condition", NpgsqlDbType.Text) { Value = opening.Condition });
            _ = command.Parameters.Add(Nullable("submission", NpgsqlDbType.Text, opening.Receipt?.Submission));
            _ = command.Parameters.Add(Nullable("bytes", NpgsqlDbType.Bigint, opening.Receipt?.Bytes));
            _ = command.Parameters.Add(Nullable("digest", NpgsqlDbType.Text, opening.Receipt?.Digest));
            _ = command.Parameters.Add(Nullable("warning", NpgsqlDbType.Text, opening.Receipt?.Warning));
            _ = command.Parameters.Add(Nullable("origin", NpgsqlDbType.Text, opening.Origin?.State));
            return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    private static async Task InsertOriginAsync(
        CustodyScope scope, int domainId, (long Batch, ReadOnlyMemory<byte> Envelope, ContentKey Key) origin, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.InsertBatchOrigin);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = domainId });
            _ = command.Parameters.Add(new NpgsqlParameter("batch", NpgsqlDbType.Bigint) { Value = origin.Batch });
            _ = command.Parameters.Add(new NpgsqlParameter("origin", NpgsqlDbType.Bytea) { Value = origin.Envelope.ToArray() });
            _ = command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = origin.Key.Epoch });
            _ = command.Parameters.Add(new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = origin.Key.Wrapped.ToArray() });
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static NpgsqlParameter Nullable(string name, NpgsqlDbType type, object? value) => new(name, type) { Value = value ?? DBNull.Value };
}
