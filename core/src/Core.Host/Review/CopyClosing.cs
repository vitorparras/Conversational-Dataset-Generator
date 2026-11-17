using Core.Persistence.Custody;
using Core.Persistence.Ingestion;

namespace Core.Host.Review;

/// <summary>The approval of the batch, with the copy of a file the domain already accepted turned back by the database and then rejected as already received.</summary>
internal sealed class CopyClosing
{
    private readonly BatchApprovals approvals;
    private readonly IngestionBatches batches;

    internal CopyClosing(BatchApprovals approvals, IngestionBatches batches)
    {
        this.approvals = approvals;
        this.batches = batches;
    }

    internal async Task<BatchReviewOutcome> ApproveAsync(BatchEntry batch, long reviewer, CancellationToken cancellationToken)
    {
        var approved = (await approvals.ApproveAsync(new BatchApproval(batch.DomainId, batch.Id, reviewer), cancellationToken).ConfigureAwait(false)).Outcome;
        if (approved != BatchReviewOutcome.AlreadyReceived)
        {
            return approved;
        }

        var closing = new BatchClosing(batch.DomainId, batch.Id, CustodyAuthor.Person(reviewer));
        var rejected = await batches.RejectAsync(closing, BatchMotives.AlreadyReceived, cancellationToken).ConfigureAwait(false);
        return rejected.Outcome == BatchOutcome.Rejected ? BatchReviewOutcome.AlreadyReceived : BatchReviewOutcome.NotRecorded;
    }
}
