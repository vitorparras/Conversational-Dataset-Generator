using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A batch that went through the review that accepts it, for the tests that are not about the review: the sample is drawn and reviewed with nothing found, by an active person, as the rule demands.
/// A batch that is not there, is closed or was sampled already is left as it is, so that a test about those cases still meets them.
/// </summary>
public static class ReviewedBatches
{
    /// <summary>Draws the sample of the batch and records a review that found nothing.</summary>
    /// <param name="dataSource">The data source with the credential of the application.</param>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch, open.</param>
    /// <param name="reviewerId">An active person.</param>
    public static async Task ReviewCleanAsync(NpgsqlDataSource dataSource, int domainId, long batchId, long reviewerId)
    {
        var reviews = new BatchReviews(dataSource, 3);

        var drawn = await reviews.DrawAsync(new BatchSampling(domainId, batchId, reviewerId), TestContext.Current.CancellationToken);
        if (drawn.Outcome == BatchReviewOutcome.Drawn)
        {
            var reviewed = await reviews.ReviewAsync(new BatchReviewing(domainId, batchId, reviewerId), TestContext.Current.CancellationToken);
            Assert.Equal(BatchReviewOutcome.Reviewed, reviewed.Outcome);
        }
    }
}
