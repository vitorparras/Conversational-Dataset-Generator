using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>Reads the state a batch holds in the transaction it is asked in, with its motive, the person who opened it and its condition.</summary>
internal static class BatchStateReading
{
    private const int StateColumn = 0;
    private const int MotiveColumn = 1;
    private const int OpenedByColumn = 2;
    private const int ConditionColumn = 3;

    /// <summary>Why the batch cannot be closed to the state asked, or null when it can.</summary>
    internal static async Task<BatchOutcome?> ClosingRefusalAsync(CustodyScope scope, long batchId, string state, CancellationToken cancellationToken)
    {
        var held = await ReadAsync(scope, batchId, cancellationToken).ConfigureAwait(false);
        return await ReviewStatements.ClosingRefusalAsync(scope, held, state, batchId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<IngestionBatch?> ReadAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ReadIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batchId });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? new IngestionBatch(
                        Enum.Parse<BatchState>(reader.GetString(StateColumn), ignoreCase: true), reader[MotiveColumn] as string, reader.GetInt64(OpenedByColumn), reader.GetString(ConditionColumn))
                    : null;
            }
        }
    }
}
