using Core.Persistence.Content;
using Npgsql;

namespace Core.Persistence.Ingestion;

/// <summary>The progress of the review of the sample (issue 822): each item is marked as reviewed in the order it was drawn, and the database refuses a mark that is not the next; the mark has no event of its own, since each text read and the review that follows are events.</summary>
public sealed class SampleMarks
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>Marks over the database of the application.</summary>
    /// <param name="dataSource">The data source with the credential of the application.</param>
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

    private static BatchReviewOutcome? PlaceRefusal(BatchReview held, int place)
    {
        if (place <= held.ReviewedPosition)
        {
            return BatchReviewOutcome.AlreadyMarked;
        }

        return place == held.ReviewedPosition + 1 && place <= held.Sample!.Count ? null : BatchReviewOutcome.PlaceNotNext;
    }

    private async Task<BatchReviewResult> MarkingAsync(BatchMarking request, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, request.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            var held = await ReviewStatements.HeldAsync(scope, request.BatchId, cancellationToken).ConfigureAwait(false);
            var refused = BatchReviews.Refusal(held, drawing: false) ?? PlaceRefusal(held!, request.Place);
            if (refused is not null)
            {
                return new BatchReviewResult { Outcome = refused.Value };
            }

            var rows = await ReviewStatements.ChangeAsync(
                scope, ContentQueries.MarkBatchPlace, cancellationToken, ReviewStatements.Id("id", request.BatchId), new NpgsqlParameter("position", request.Place)).ConfigureAwait(false);
            if (rows != 1)
            {
                return new BatchReviewResult { Outcome = BatchReviewOutcome.AlreadyMarked };
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new BatchReviewResult { Outcome = BatchReviewOutcome.Marked };
        }
    }
}
