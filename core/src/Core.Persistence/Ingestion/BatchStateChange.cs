using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// Writes the closing of a batch in the transaction that appends its event: the state and the motive, and, for a rejection with a motive,
/// the removal of what the batch holds (the marks, the occurrences, the maps and the items, in that order).
/// </summary>
/// <param name="BatchId">The batch.</param>
/// <param name="State">The state it closes in.</param>
/// <param name="Motive">The motive of a rejection; none for an acceptance.</param>
internal sealed record BatchStateChange(long BatchId, string State, string? Motive)
{
    /// <summary>Sets the state and the motive of the batch and, when there is a motive, removes what it holds.</summary>
    /// <param name="scope">The transaction that closes the batch.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    internal async Task ChangeAsync(CustodyScope scope, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.CloseIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("state", NpgsqlDbType.Text) { Value = State });
            _ = command.Parameters.Add(new NpgsqlParameter("motive", NpgsqlDbType.Text) { Value = (object?)Motive ?? DBNull.Value });
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = BatchId });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Motive is not null)
        {
            await RemoveAsync(scope, BatchId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RemoveAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
    {
        await RemoveAsync(scope, ContentQueries.RemoveBatchMarks, batchId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync(scope, ContentQueries.RemoveBatchOccurrences, batchId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync(scope, ContentQueries.RemoveBatchMaps, batchId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync(scope, ContentQueries.RemoveBatchItems, batchId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemoveAsync(CustodyScope scope, string statement, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = BatchId });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
