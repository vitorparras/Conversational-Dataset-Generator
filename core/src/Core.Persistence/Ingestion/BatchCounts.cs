using System.Data;
using Core.Persistence.Content;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The numbers of a batch for its detail (issue 821), read by a function of the database that answers numbers and codes alone (migration 0057), so the reading needs no scope of the system and opens
/// no content; since issue 842 also the version of the steps that sanitized the batch, with what each step did. The database judges the domain, so a batch of another domain reads
/// as one with no number.
/// </summary>
public sealed class BatchCounts
{
    private const int KindColumn = 0;
    private const int CodeColumn = 1;
    private const int ValueColumn = 2;
    private const int ItemsColumn = 3;

    private static readonly string[] DestinationCodes = Codes("remove", "pseudonymize", "preserve");

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The reading over the database.</summary>
    /// <param name="dataSource">The source of the connections, with the privileges of the application.</param>
    public BatchCounts(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The numbers of the batch in the domain.</summary>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<BatchDetailCounts> ReadAsync(int domainId, long batchId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return ReadingAsync(domainId, batchId, cancellationToken);
    }

    private async Task<BatchDetailCounts> ReadingAsync(int domainId, long batchId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var rows = await RowsAsync(transaction, BatchCountStatements.Read, (domainId, batchId), Count, cancellationToken).ConfigureAwait(false);
            var steps = await RowsAsync(transaction, BatchCountStatements.Steps, (domainId, batchId), Step, cancellationToken).ConfigureAwait(false);
            var version = steps.Count == 0 ? null : steps[0].Version;
            return Counted(rows) with { SanitizationVersion = version, Steps = [.. steps.Where(step => step.Version == version).Select(step => step.Count)] };
        }
    }

    /// <summary>The rows a statement answers about the batch of the domain, each read by the function given.</summary>
    private static async Task<List<T>> RowsAsync<T>(
        ContentTransaction transaction, string statement, (int Domain, long Batch) of, Func<IDataRecord, T> read, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("batch", NpgsqlDbType.Bigint) { Value = of.Batch });
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = of.Domain });
            var rows = new List<T>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(read(reader));
                }
            }

            return rows;
        }
    }

    private static (string Kind, BatchCount Count) Count(IDataRecord reader) =>
        (reader.GetString(KindColumn), new BatchCount(reader.GetString(CodeColumn), reader.GetInt64(ValueColumn)));

    private static (string Version, BatchStepCount Count) Step(IDataRecord reader) =>
        (reader.GetString(KindColumn), new BatchStepCount(reader.GetString(CodeColumn), reader.GetInt64(ValueColumn), reader.GetInt64(ItemsColumn)));

    private static BatchDetailCounts Counted(List<(string Kind, BatchCount Count)> rows)
    {
        var destinations = DestinationCodes.Select(code => Destined(rows, code)).ToList();
        return new BatchDetailCounts
        {
            Forms = Of(rows, "form"),
            Destinations = destinations,
            Accommodations = Of(rows, "loss"),
            Marked = Replica(rows, "marked"),
            Firsts = Replica(rows, "firsts"),
        };
    }

    private static List<BatchCount> Of(List<(string Kind, BatchCount Count)> rows, string kind)
    {
        var found = rows.Where(row => row.Kind == kind).Select(row => row.Count).OrderBy(count => count.Code, StringComparer.Ordinal).ToList();
        return found;
    }

    private static BatchCount Destined(List<(string Kind, BatchCount Count)> rows, string code) =>
        rows.Find(row => row.Kind == "destination" && row.Count.Code == code).Count ?? new BatchCount(code, 0);

    private static string[] Codes(params string[] listed) => listed;

    private static long Replica(List<(string Kind, BatchCount Count)> rows, string code) =>
        rows.Find(row => row.Kind == "replica" && row.Count.Code == code).Count?.Value ?? 0;
}
