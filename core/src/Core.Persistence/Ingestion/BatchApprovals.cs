using System.Runtime.CompilerServices;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The approval of a batch after the review of its whole sample found nothing (issue 822): the review and the acceptance
/// are two events of the chain and two changes of the row in one transaction, so that a batch is never reviewed and left open
/// by an approval, nor accepted without the review. When the material is already in an accepted batch of the domain, the batch is not accepted.
/// </summary>
public sealed class BatchApprovals(NpgsqlDataSource dataSource)
{
    /// <summary>Records the clean review of the whole sample and accepts the batch with it.</summary>
    /// <param name="request">The batch and the person who approves.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<BatchReviewResult> ApproveAsync(BatchApproval request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ReviewerId);
        return BatchRetry.RunAsync(() => ApprovingAsync(request, cancellationToken), result => result.Collided, BatchRetry.Attempts, cancellationToken);
    }

    private static async Task<CustodyResult> AppendAsync(CustodyScope scope, BatchApproval request, CancellationToken cancellationToken)
    {
        var reviewed = await CustodyAppend.AppendAsync(scope, CustodyEvent.BatchReviewed(request.ReviewerId, request.BatchId, found: false), cancellationToken).ConfigureAwait(false);
        return reviewed.Outcome == CustodyOutcome.Appended
            ? await CustodyAppend.AppendAsync(scope, CustodyEvent.BatchAccepted(CustodyAuthor.Person(request.ReviewerId), request.BatchId), cancellationToken).ConfigureAwait(false)
            : reviewed;
    }

    private static async Task<BatchReviewOutcome?> RefusalAsync(CustodyScope scope, BatchApproval request, CancellationToken cancellationToken)
    {
        await ReviewStatements.DeclareDomainAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        var held = await ReviewStatements.HeldAsync(scope, request.BatchId, cancellationToken).ConfigureAwait(false);
        return BatchReviews.Refusal(held, drawing: false) ?? (held!.ReviewedPosition < held.Sample!.Count ? BatchReviewOutcome.ReviewIncomplete : null);
    }

    private static async Task<BatchReviewOutcome?> CloseAsync(CustodyScope scope, BatchApproval request, CancellationToken cancellationToken)
    {
        var rows = await ReviewStatements.ChangeAsync(
            scope, ContentQueries.CloseBatchReviewClean, cancellationToken, ReviewStatements.Id("id", request.BatchId), ReviewStatements.Id("reviewer", request.ReviewerId)).ConfigureAwait(false);
        if (rows != 1)
        {
            return BatchReviewOutcome.AlreadyReviewed;
        }

        try
        {
            await new BatchStateChange(request.BatchId, ReviewStatements.AcceptedState, Motive: null).ChangeAsync(scope, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (PostgresException repeated) when (repeated.SqlState == PostgresErrorCodes.UniqueViolation && repeated.ConstraintName == IngestionBatches.AcceptedDigestIndex)
        {
            return BatchReviewOutcome.AlreadyReceived;
        }
    }

    private static async Task<CustodyResult?> ApproveInAsync(CustodyScope scope, BatchApproval request, StrongBox<BatchReviewOutcome?> early, CancellationToken cancellationToken)
    {
        early.Value = await RefusalAsync(scope, request, cancellationToken).ConfigureAwait(false);
        if (early.Value is not null)
        {
            return null;
        }

        var appended = await AppendAsync(scope, request, cancellationToken).ConfigureAwait(false);
        if (appended.Outcome != CustodyOutcome.Appended)
        {
            return appended;
        }

        early.Value = await CloseAsync(scope, request, cancellationToken).ConfigureAwait(false);
        return early.Value is null ? await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false) : null;
    }

    private async Task<BatchReviewResult> ApprovingAsync(BatchApproval request, CancellationToken cancellationToken)
    {
        var early = new StrongBox<BatchReviewOutcome?>(null);
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => ApproveInAsync(scope, request, early, cancellationToken), cancellationToken).ConfigureAwait(false);
        return early.Value is { } outcome ? new BatchReviewResult { Outcome = outcome } : BatchReviews.Answered(recorded, BatchReviewOutcome.Approved, null);
    }
}
