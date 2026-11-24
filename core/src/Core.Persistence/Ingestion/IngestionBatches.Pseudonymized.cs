using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>The event of the pseudonymization of a batch, which goes to the chain only with the acceptance that succeeds, in its transaction (issue 101).</summary>
public sealed partial class IngestionBatches
{
    /// <summary>
    /// The event of the closing; and, when a batch is accepted with some item pseudonymized, the event of the pseudonymization before it, which the database asks for
    /// to accept such a batch. Neither stays in the chain when the acceptance does not commit, so the chain never tells of a pseudonymization that did not happen.
    /// </summary>
    private static async Task<CustodyResult> AppendClosingAsync(CustodyScope scope, Closing closing, CancellationToken cancellationToken)
    {
        if (closing.State == "accepted" && await PseudonymizedAsync(scope, closing.Request.BatchId, cancellationToken).ConfigureAwait(false))
        {
            var pseudonymized = CustodyEvent.BatchPseudonymized(closing.Request.Author, closing.Request.BatchId);
            var appended = await CustodyAppend.AppendAsync(scope, pseudonymized, cancellationToken).ConfigureAwait(false);
            if (appended.Outcome != CustodyOutcome.Appended)
            {
                return appended;
            }
        }

        return await CustodyAppend.AppendAsync(scope, closing.Event, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> PseudonymizedAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ReadBatchPseudonymized);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batchId });
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
