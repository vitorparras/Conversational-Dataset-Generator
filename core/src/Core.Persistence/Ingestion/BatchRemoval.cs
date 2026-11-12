using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>Removes what a batch rejected for a motive holds, in the transaction that rejects it: the marks, the occurrences, the maps and the items, in that order.</summary>
internal static class BatchRemoval
{
    /// <summary>Removes the four kinds of row of the batch.</summary>
    /// <param name="scope">The transaction that rejects the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    internal static async Task RemoveAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
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
