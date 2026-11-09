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
/// The sample of a batch for review (issue 83): the system draws it, with a generator that cannot be guessed and without the one who asks choosing an item or how many.
/// The draw is an event of the chain, written in the very transaction that writes the sample, and the sample, once written, is never changed.
/// </summary>
public sealed class BatchReviews
{
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

    private static BatchReviewOutcome? Refusal(BatchReview? held)
    {
        if (held is null)
        {
            return BatchReviewOutcome.NotFound;
        }

        if (held.State != BatchState.Open)
        {
            return BatchReviewOutcome.NotOpen;
        }

        return held.Sample is null ? null : BatchReviewOutcome.AlreadySampled;
    }

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
        if (Refusal(held) is { } refused)
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

    private async Task<BatchReviewResult> DrawingAsync(BatchSampling request, CancellationToken cancellationToken)
    {
        var run = new StrongBox<(BatchReviewOutcome? Early, long[]? Sample)>();
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => DrawInAsync(scope, request, run, cancellationToken), cancellationToken).ConfigureAwait(false);
        return run.Value.Early is { } early ? new BatchReviewResult { Outcome = early } : Answered(recorded, BatchReviewOutcome.Drawn, run.Value.Sample);
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
