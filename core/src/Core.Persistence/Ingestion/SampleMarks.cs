using Core.Persistence.Content;
using Npgsql;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The progress of the review of the sample (#822): each item is marked reviewed in the order of the draw, and the database refuses a mark that is not the next, and the mark of an item
/// without the pseudonymized text (#939).
/// </summary>
public sealed class SampleMarks
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>Marks over the database of the application.</summary>
    public SampleMarks(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    /// <summary>Marks the place of the sample as reviewed, when it is the one right after the last marked.</summary>
    /// <param name="request">The batch and the place to mark.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<BatchReviewResult> MarkAsync(BatchMarking request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Place);
        return MarkingAsync(request, cancellationToken);
    }

    private async Task<BatchReviewResult> MarkingAsync(BatchMarking request, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, request.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            var held = await ReviewStatements.HeldAsync(scope, request.BatchId, cancellationToken).ConfigureAwait(false);
            var refused = BatchReviews.Refusal(held, drawing: false);
            if (refused is not null)
            {
                return new BatchReviewResult { Outcome = refused.Value };
            }

            if (!await ReviewStatements.PseudonymizedAsync(scope, request.BatchId, request.Place, cancellationToken).ConfigureAwait(false))
            {
                return new BatchReviewResult { Outcome = BatchReviewOutcome.NotPseudonymized };
            }

            var rows = await ReviewStatements.ChangeAsync(
                scope, ContentQueries.CloseBatchPlace, cancellationToken, ReviewStatements.Id("id", request.BatchId), new NpgsqlParameter("position", request.Place)).ConfigureAwait(false);
            if (rows != 1)
            {
                return new BatchReviewResult { Outcome = BatchReviewOutcome.AlreadyMarked };
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new BatchReviewResult { Outcome = BatchReviewOutcome.Marked };
        }
    }
}
