using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;

namespace Core.Persistence.Ingestion;

/// <summary>
/// The review of a batch by sample (issue 83): the system draws the sample, with a generator that cannot be guessed and without the one who reviews choosing an item,
/// a person reviews it, and one finding of personal data not treated rejects the whole batch. Each step is an event of the chain, written in the very transaction
/// that changes the batch, and the database refuses the acceptance of a batch whose sample was not reviewed.
/// </summary>
public sealed class BatchReviews
{
    /// <summary>The motive with which a finding rejects the batch, the same in the row and in the chain.</summary>
    public const string RejectionMotive = "review_found_personal_data";

    private readonly NpgsqlDataSource dataSource;
    private readonly int? sampleSize;

    /// <summary>Reviews over the database of the application, with the size of the sample the installation declares.</summary>
    /// <param name="dataSource">The data source with the credential of the application.</param>
    /// <param name="sampleSize">How many items a sample holds at most; a batch with fewer is sampled whole. Null when the installation declares none, and then nothing is drawn.</param>
    public BatchReviews(NpgsqlDataSource dataSource, int? sampleSize)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleSize ?? 1);
        this.dataSource = dataSource;
        this.sampleSize = sampleSize;
    }

    /// <summary>Draws the sample of an open batch, once, and writes it before the review begins.</summary>
    /// <param name="request">The batch and the person who asks.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<BatchReviewResult> DrawAsync(BatchSampling request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ReviewerId);
        return sampleSize is null ? Task.FromResult(new BatchReviewResult { Outcome = BatchReviewOutcome.SizeNotDeclared }) : DrawingAsync(request, cancellationToken);
    }

    /// <summary>Records the review of the sample: a clean one lets the batch be accepted, and a finding rejects the whole batch, with its material, in the same transaction.</summary>
    /// <param name="request">The batch, the reviewer and the finding, if there is one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<BatchReviewResult> ReviewAsync(BatchReviewing request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ReviewerId);
        return request.Finding is not null && !CustodyEvent.IsBatchMotive(request.Finding)
            ? Task.FromResult(new BatchReviewResult { Outcome = BatchReviewOutcome.FindingRefused })
            : ReviewingAsync(request, cancellationToken);
    }

    /// <summary>The review of a batch as it stands, whatever the state of the batch; null when the domain has no such batch.</summary>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<BatchReview?> ReadAsync(int domainId, long batchId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return ReadingAsync(domainId, batchId, cancellationToken);
    }

    private static BatchReviewOutcome? Refusal(BatchReview? held, bool drawing)
    {
        if (held is null)
        {
            return BatchReviewOutcome.NotFound;
        }

        if (held.State != BatchState.Open)
        {
            return BatchReviewOutcome.NotOpen;
        }

        return drawing ? (held.Sample is null ? null : BatchReviewOutcome.AlreadySampled) : ReviewRefusal(held);
    }

    private static BatchReviewOutcome? ReviewRefusal(BatchReview held) =>
        held.Sample is null ? BatchReviewOutcome.NotSampled : (held.ReviewerId is null ? null : BatchReviewOutcome.AlreadyReviewed);

    private static long[] Drawn(long[] numbers, int size)
    {
        var shuffled = (long[])numbers.Clone();
        RandomNumberGenerator.Shuffle(shuffled.AsSpan());
        var drawn = shuffled.Take(size).ToArray();
        Array.Sort(drawn);
        return drawn;
    }

    private static BatchReviewResult Answered(CustodyResult? recorded, BatchReviewOutcome done, IReadOnlyList<long>? sample) =>
        recorded?.Outcome == CustodyOutcome.Appended
            ? new BatchReviewResult { Outcome = done, Sample = sample, Sequence = recorded.Sequence }
            : new BatchReviewResult { Outcome = BatchReviewOutcome.NotRecorded, Refusal = recorded };

    private static async Task<BatchReviewOutcome?> DrawRefusalAsync(CustodyScope scope, BatchSampling request, StrongBox<long[]> numbers, CancellationToken cancellationToken)
    {
        await ReviewStatements.DeclareDomainAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        var held = await ReviewStatements.HeldAsync(scope, request.BatchId, cancellationToken).ConfigureAwait(false);
        if (Refusal(held, drawing: true) is { } refused)
        {
            return refused;
        }

        numbers.Value = await ReviewStatements.NumbersAsync(scope, request.DomainId, request.BatchId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<CustodyResult?> DrawInAsync(
        CustodyScope scope, BatchSampling request, StrongBox<(BatchReviewOutcome? Early, long[]? Sample)> run, CancellationToken cancellationToken)
    {
        var numbers = new StrongBox<long[]>();
        if (await DrawRefusalAsync(scope, request, numbers, cancellationToken).ConfigureAwait(false) is { } early)
        {
            run.Value = (early, null);
            return null;
        }

        var sample = Drawn(numbers.Value, sampleSize.GetValueOrDefault());
        run.Value = (null, sample);
        var appended = await CustodyAppend.AppendAsync(scope, CustodyEvent.BatchSampled(request.ReviewerId, request.BatchId), cancellationToken).ConfigureAwait(false);
        if (appended.Outcome != CustodyOutcome.Appended)
        {
            return appended;
        }

        var written = string.Join(',', sample.Select(number => number.ToString(CultureInfo.InvariantCulture)));
        var rows = await ReviewStatements.ChangeAsync(
            scope, ReviewQueries.Draw, cancellationToken, ReviewStatements.Id("id", request.BatchId), new NpgsqlParameter("sample", written)).ConfigureAwait(false);
        if (rows == 1)
        {
            return await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false);
        }

        run.Value = (BatchReviewOutcome.AlreadySampled, null);
        return null;
    }

    private static async Task<CustodyResult?> ReviewInAsync(CustodyScope scope, BatchReviewing request, StrongBox<BatchReviewOutcome?> early, CancellationToken cancellationToken)
    {
        await ReviewStatements.DeclareDomainAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        var held = await ReviewStatements.HeldAsync(scope, request.BatchId, cancellationToken).ConfigureAwait(false);
        if (Refusal(held, drawing: false) is { } refused)
        {
            early.Value = refused;
            return null;
        }

        var appended = await AppendAsync(scope, request, cancellationToken).ConfigureAwait(false);
        if (appended.Outcome != CustodyOutcome.Appended)
        {
            return appended;
        }

        if (await RecordAsync(scope, request, cancellationToken).ConfigureAwait(false) == 1)
        {
            return await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false);
        }

        early.Value = BatchReviewOutcome.AlreadyReviewed;
        return null;
    }

    private static async Task<CustodyResult> AppendAsync(CustodyScope scope, BatchReviewing request, CancellationToken cancellationToken)
    {
        var found = request.Finding is not null;
        var reviewed = await CustodyAppend.AppendAsync(scope, CustodyEvent.BatchReviewed(request.ReviewerId, request.BatchId, found), cancellationToken).ConfigureAwait(false);
        return found && reviewed.Outcome == CustodyOutcome.Appended
            ? await CustodyAppend.AppendAsync(scope, CustodyEvent.BatchRejected(CustodyAuthor.Person(request.ReviewerId), request.BatchId, RejectionMotive), cancellationToken).ConfigureAwait(false)
            : reviewed;
    }

    private static async Task<int> RecordAsync(CustodyScope scope, BatchReviewing request, CancellationToken cancellationToken)
    {
        var batch = ReviewStatements.Id("id", request.BatchId);
        var reviewer = ReviewStatements.Id("reviewer", request.ReviewerId);
        if (request.Finding is null)
        {
            return await ReviewStatements.ChangeAsync(scope, ReviewQueries.ReviewClean, cancellationToken, batch, reviewer).ConfigureAwait(false);
        }

        var finding = new NpgsqlParameter("finding", request.Finding);
        var motive = new NpgsqlParameter("motive", RejectionMotive);
        var rows = await ReviewStatements.ChangeAsync(scope, ReviewQueries.ReviewFound, cancellationToken, batch, reviewer, finding, motive).ConfigureAwait(false);
        _ = await ReviewStatements.ChangeAsync(scope, ContentQueries.RemoveBatchMaps, cancellationToken, ReviewStatements.Id("id", request.BatchId)).ConfigureAwait(false);
        _ = await ReviewStatements.ChangeAsync(scope, ContentQueries.RemoveBatchItems, cancellationToken, ReviewStatements.Id("id", request.BatchId)).ConfigureAwait(false);
        return rows;
    }

    private async Task<BatchReviewResult> DrawingAsync(BatchSampling request, CancellationToken cancellationToken)
    {
        var run = new StrongBox<(BatchReviewOutcome? Early, long[]? Sample)>();
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => DrawInAsync(scope, request, run, cancellationToken), cancellationToken).ConfigureAwait(false);
        return run.Value.Early is { } early ? new BatchReviewResult { Outcome = early } : Answered(recorded, BatchReviewOutcome.Drawn, run.Value.Sample);
    }

    private async Task<BatchReviewResult> ReviewingAsync(BatchReviewing request, CancellationToken cancellationToken)
    {
        var early = new StrongBox<BatchReviewOutcome?>(null);
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => ReviewInAsync(scope, request, early, cancellationToken), cancellationToken).ConfigureAwait(false);
        var done = request.Finding is null ? BatchReviewOutcome.Reviewed : BatchReviewOutcome.Rejected;
        return early.Value is { } outcome ? new BatchReviewResult { Outcome = outcome } : Answered(recorded, done, null);
    }

    private async Task<BatchReview?> ReadingAsync(int domainId, long batchId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ReviewStatements.HeldAsync(transaction.AsCustodyScope(), batchId, cancellationToken).ConfigureAwait(false);
        }
    }
}
