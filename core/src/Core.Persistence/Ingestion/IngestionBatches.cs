using System.Globalization;
using System.Runtime.CompilerServices;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The batch of ingestion: the unit in which material comes in and goes out. It opens with no content (no signature here takes a byte), takes content while it is open,
/// and closes once, accepted or rejected. The opening and the closing are each written with the event of the chain in one serializable transaction, and the database
/// refuses either without its event; a rejection removes the content of the batch in that same transaction, so that no partial material stays reachable.
/// What the batch holds is not readable as stock until it is accepted: the database sees to that, not this class.
/// </summary>
public sealed class IngestionBatches
{
    private const int StateColumn = 0;
    private const int MotiveColumn = 1;
    private const int OpenedByColumn = 2;
    private const int ConditionColumn = 3;
    private const string AuthorConstraint = "ingestion_batch_opened_by_fkey";

    private readonly NpgsqlDataSource dataSource;
    private readonly OriginConditions conditions;

    /// <summary>The batches are written through the data source built with the application's credential, and open under one of the conditions the installation knows.</summary>
    /// <param name="dataSource">The application's data source.</param>
    /// <param name="conditions">The conditions the installation knows, the most restrictive first.</param>
    public IngestionBatches(NpgsqlDataSource dataSource, OriginConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(conditions);
        this.dataSource = dataSource;
        this.conditions = conditions;
    }

    /// <summary>
    /// Opens a batch in the domain, with no content, by a person who must be active; the event of the opening is written with it. The batch takes the condition the opening declares, which must be one the installation
    /// knows; when it declares none, the batch takes the most restrictive one, and never the most permissive.
    /// </summary>
    /// <param name="opening">The domain, the person and the condition, if declared.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    public Task<BatchResult> OpenAsync(BatchOpening opening, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opening.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opening.AuthorId);
        return opening.Condition is { } declared && conditions.RankOf(declared) < 0
            ? Task.FromResult(new BatchResult { Outcome = BatchOutcome.ConditionUnknown })
            : OpeningAsync(opening with { Condition = opening.Condition ?? conditions.MostRestrictive }, cancellationToken);
    }

    /// <summary>Accepts an open batch: what it holds becomes stock, and the event of the closing is written with it.</summary>
    /// <param name="closing">The batch, its domain and who accepts it.</param>
    /// <param name="cancellationToken">Cancels the closing.</param>
    public Task<BatchResult> AcceptAsync(BatchClosing closing, CancellationToken cancellationToken)
    {
        Check(closing);
        return ClosingAsync(new Closing(closing, CustodyEvent.BatchAccepted(closing.Author, closing.BatchId), "accepted", null), cancellationToken);
    }

    /// <summary>Rejects an open batch: its content is removed in the same transaction, and the batch stays with its motive and the two events of the chain.</summary>
    /// <param name="closing">The batch, its domain and who rejects it.</param>
    /// <param name="motive">Why, as a name of lowercase letters, digits and underscores; never free text.</param>
    /// <param name="cancellationToken">Cancels the closing.</param>
    public Task<BatchResult> RejectAsync(BatchClosing closing, string motive, CancellationToken cancellationToken)
    {
        Check(closing);
        return CustodyEvent.IsBatchMotive(motive)
            ? ClosingAsync(new Closing(closing, CustodyEvent.BatchRejected(closing.Author, closing.BatchId, motive), "rejected", motive), cancellationToken)
            : Task.FromResult(new BatchResult { Outcome = BatchOutcome.MotiveRefused });
    }

    /// <summary>The batch as the database holds it in the domain, whatever its state; null when the domain holds no such batch.</summary>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IngestionBatch?> ReadAsync(int domainId, long batchId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return ReadingAsync(domainId, batchId, cancellationToken);
    }

    private static void Check(BatchClosing closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(closing.Author);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(closing.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(closing.BatchId);
    }

    private static BatchResult Answered(CustodyResult? recorded, BatchOutcome done, long batchId) =>
        recorded?.Outcome == CustodyOutcome.Appended
            ? new BatchResult { Outcome = done, BatchId = batchId, Sequence = recorded.Sequence }
            : new BatchResult { Outcome = BatchOutcome.NotRecorded, Refusal = recorded };

    private static async Task DeclareDomainAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentTransaction.Declare);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("domain", domainId.ToString(CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static NpgsqlParameter Id(string name, long value) => new(name, NpgsqlDbType.Bigint) { Value = value };

    private async Task<BatchResult> OpeningAsync(BatchOpening opening, CancellationToken cancellationToken)
    {
        var run = new StrongBox<(long Batch, BatchOutcome? Failure)>();
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => OpenInAsync(scope, opening, run, cancellationToken), cancellationToken).ConfigureAwait(false);
        return run.Value.Failure is { } outcome ? new BatchResult { Outcome = outcome } : Answered(recorded, BatchOutcome.Opened, run.Value.Batch);
    }

    private static async Task<CustodyResult?> OpenInAsync(
        CustodyScope scope, BatchOpening opening, StrongBox<(long Batch, BatchOutcome? Failure)> run, CancellationToken cancellationToken)
    {
        await DeclareDomainAsync(scope, opening.DomainId, cancellationToken).ConfigureAwait(false);
        try
        {
            run.Value = (await InsertAsync(scope, opening, cancellationToken).ConfigureAwait(false), null);
        }
        catch (PostgresException refused) when (refused.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            run.Value = (0, refused.ConstraintName == AuthorConstraint ? BatchOutcome.AuthorUnknown : BatchOutcome.DomainUnknown);
            return null;
        }

        var appended = await CustodyAppend.AppendAsync(scope, CustodyEvent.BatchOpened(opening.AuthorId, run.Value.Batch), cancellationToken).ConfigureAwait(false);
        return await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> InsertAsync(CustodyScope scope, BatchOpening opening, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.InsertIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = opening.DomainId });
            _ = command.Parameters.Add(Id("author", opening.AuthorId));
            _ = command.Parameters.Add(new NpgsqlParameter("condition", NpgsqlDbType.Text) { Value = opening.Condition });
            return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    private async Task<BatchResult> ClosingAsync(Closing closing, CancellationToken cancellationToken)
    {
        var early = new StrongBox<BatchOutcome?>(null);
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => CloseInAsync(scope, closing, early, cancellationToken), cancellationToken).ConfigureAwait(false);
        var done = closing.State == "accepted" ? BatchOutcome.Accepted : BatchOutcome.Rejected;
        return early.Value is { } outcome ? new BatchResult { Outcome = outcome } : Answered(recorded, done, closing.Request.BatchId);
    }

    /// <summary>The state is looked at first, so that a batch that does not exist or is closed leaves no event; the event comes before the change, which the database refuses without it.</summary>
    private static async Task<CustodyResult?> CloseInAsync(CustodyScope scope, Closing closing, StrongBox<BatchOutcome?> early, CancellationToken cancellationToken)
    {
        await DeclareDomainAsync(scope, closing.Request.DomainId, cancellationToken).ConfigureAwait(false);
        var held = await ReadStateAsync(scope, closing.Request.BatchId, cancellationToken).ConfigureAwait(false);
        if (held is not { } current || current.State != BatchState.Open)
        {
            early.Value = held is null ? BatchOutcome.NotFound : BatchOutcome.AlreadyClosed;
            return null;
        }

        var appended = await CustodyAppend.AppendAsync(scope, closing.Event, cancellationToken).ConfigureAwait(false);
        if (appended.Outcome != CustodyOutcome.Appended)
        {
            return appended;
        }

        await ChangeAsync(scope, closing, cancellationToken).ConfigureAwait(false);
        return await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ChangeAsync(CustodyScope scope, Closing closing, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.CloseIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("state", NpgsqlDbType.Text) { Value = closing.State });
            _ = command.Parameters.Add(new NpgsqlParameter("motive", NpgsqlDbType.Text) { Value = (object?)closing.Motive ?? DBNull.Value });
            _ = command.Parameters.Add(Id("id", closing.Request.BatchId));
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (closing.Motive is not null)
        {
            await RemoveAsync(scope, ContentQueries.RemoveBatchMaps, closing.Request.BatchId, cancellationToken).ConfigureAwait(false);
            await RemoveAsync(scope, ContentQueries.RemoveBatchItems, closing.Request.BatchId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RemoveAsync(CustodyScope scope, string statement, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(Id("id", batchId));
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IngestionBatch?> ReadStateAsync(CustodyScope scope, long batchId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.ReadIngestionBatch);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(Id("id", batchId));
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? new IngestionBatch(StateOf(reader.GetString(StateColumn)), reader[MotiveColumn] as string, reader.GetInt64(OpenedByColumn), reader.GetString(ConditionColumn))
                    : null;
            }
        }
    }

    private static BatchState StateOf(string state) => Enum.Parse<BatchState>(state, ignoreCase: true);

    private async Task<IngestionBatch?> ReadingAsync(int domainId, long batchId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ReadStateAsync(transaction.AsCustodyScope(), batchId, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record Closing(BatchClosing Request, CustodyEvent Event, string State, string? Motive);
}
