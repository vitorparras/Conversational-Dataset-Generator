using Core.Persistence.Content;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>Reads, in one domain, where an item came from: the batch that kept it, when the submission came and who sent it.</summary>
internal static class ProvenanceRead
{
    private const int BatchColumn = 0;
    private const int SubmittedAtColumn = 1;
    private const int SubmittedByColumn = 2;
    private const int SubmissionColumn = 3;

    /// <summary>The provenance of the item in the domain; null when the domain does not hold it.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="domain">The domain the database judges.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<ItemProvenance?> InAsync(NpgsqlDataSource dataSource, int domain, long itemId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(ContentQueries.ReadItemProvenance);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = itemId });
                return await ReadAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<ItemProvenance?> ReadAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var submittedAt = await reader.GetFieldValueAsync<DateTimeOffset>(SubmittedAtColumn, cancellationToken).ConfigureAwait(false);
            return new ItemProvenance(reader.GetInt64(BatchColumn), submittedAt, reader.GetInt64(SubmittedByColumn), reader[SubmissionColumn] as string);
        }
    }
}
