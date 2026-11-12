using System.Globalization;
using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>What the review of a batch asks of the database inside a transaction of the chain: declare the domain, read the batch, list the items, change the row.</summary>
internal static class ReviewStatements
{
    internal const string AcceptedState = "accepted";
    private const int StateColumn = 0;
    private const int SampleColumn = 1;
    private const int SampledAtColumn = 2;
    private const int ReviewerColumn = 3;
    private const int ReviewedAtColumn = 4;
    private const int FindingColumn = 5;

    internal static NpgsqlParameter Id(string name, long value) => new(name, NpgsqlDbType.Bigint) { Value = value };

    internal static async Task DeclareDomainAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentTransaction.Declare);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("domain", domainId.ToString(CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<int> ChangeAsync(CustodyScope scope, string statement, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            foreach (var parameter in parameters)
            {
                _ = command.Parameters.Add(parameter);
            }

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<long[]> NumbersAsync(CustodyScope scope, int domainId, long batchId, CancellationToken cancellationToken)
    {
        _ = await ChangeAsync(scope, ReviewQueries.TakeTurn, cancellationToken, Id("id", batchId)).ConfigureAwait(false);
        var command = scope.CreateCommand(ReviewQueries.ItemNumbers);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(Id("id", batchId));
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = domainId });
            var listed = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return listed is long[] numbers ? numbers : throw new InvalidOperationException("The list of the items of the batch did not come back as numbers.");
        }
    }

    internal static async Task<BatchReview?> HeldAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ReadBatchReview);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(Id("id", batchId));
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Row(reader) : null;
            }
        }
    }

    /// <summary>Why an open batch cannot be accepted, or null when it can: it would be accepted without a review that found nothing.</summary>
    /// <param name="scope">The transaction that closes the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<BatchOutcome?> AcceptanceRefusalAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
    {
        var review = await HeldAsync(scope, batchId, cancellationToken).ConfigureAwait(false);
        return review is { ReviewerId: not null, Finding: null } ? null : BatchOutcome.ReviewMissing;
    }

    private static List<long> Numbers(string sample) => [.. sample.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(number => long.Parse(number, CultureInfo.InvariantCulture))];

    private static DateTimeOffset? Moment(NpgsqlDataReader reader, int column) => reader.IsDBNull(column) ? null : reader.GetFieldValue<DateTimeOffset>(column);

    private static BatchReview Row(NpgsqlDataReader reader) => new(
        Enum.Parse<BatchState>(reader.GetString(StateColumn), ignoreCase: true),
        reader[SampleColumn] is string sample ? Numbers(sample) : null,
        Moment(reader, SampledAtColumn),
        reader[ReviewerColumn] as long?,
        Moment(reader, ReviewedAtColumn),
        reader[FindingColumn] as string);
}
