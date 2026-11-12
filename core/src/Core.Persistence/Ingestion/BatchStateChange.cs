using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// Writes the closing of a batch in the transaction that appends its event: the state and the motive, and, for a rejection with a motive,
/// the removal of what the batch holds (the marks, the occurrences, the maps and the items, in that order).
/// </summary>
internal static class BatchStateChange
{
    /// <summary>Sets the state and the motive of the batch and, when there is a motive, removes what it holds.</summary>
    /// <param name="scope">The transaction that closes the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="state">The state it closes in.</param>
    /// <param name="motive">The motive of a rejection; none for an acceptance.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    internal static async Task ChangeAsync(CustodyScope scope, long batchId, string state, string? motive, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.CloseIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("state", NpgsqlDbType.Text) { Value = state });
            _ = command.Parameters.Add(new NpgsqlParameter("motive", NpgsqlDbType.Text) { Value = (object?)motive ?? DBNull.Value });
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batchId });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (motive is not null)
        {
            await RemoveAsync(scope, batchId, cancellationToken).ConfigureAwait(false);
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
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batchId });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
