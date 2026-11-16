using System.Globalization;
using Core.Host.Screens;
using Core.Ingestion.Review;
using Core.Persistence.Ingestion;
using Core.Persistence.People;

namespace Core.Host.Review;

/// <summary>The review of the sample (#610) as the screens ask for it: the domain decides whether a step is allowed before it writes, and the database decides again.</summary>
internal sealed class SampleReviewDesk
{
    private const string Removed = "Pessoa removida";

    private readonly (BatchReviews Reviews, SampleMarks Marks, BatchApprovals Approvals) steps;
    private readonly (PersonRegistry Registry, Func<CancellationToken, Task<SampleTextAccess>> Texts, int? Declared) around;

    internal SampleReviewDesk(
        (BatchReviews Reviews, SampleMarks Marks, BatchApprovals Approvals) steps,
        (PersonRegistry Registry, Func<CancellationToken, Task<SampleTextAccess>> Texts, int? Declared) around)
    {
        ArgumentNullException.ThrowIfNull(around.Registry);
        ArgumentNullException.ThrowIfNull(around.Texts);
        this.steps = steps;
        this.around = around;
    }

    /// <summary>The size the installation declares for the sample; null when it declares none, and then no sample can be drawn.</summary>
    internal int? Declared => around.Declared;

    /// <summary>Draws the sample of the batch for the reviewer, once.</summary>
    internal Task<BatchReviewResult> DrawAsync(BatchEntry batch, long reviewer, CancellationToken cancellationToken) =>
        steps.Reviews.DrawAsync(new BatchSampling(batch.DomainId, batch.Id, reviewer), cancellationToken);

    /// <summary>Marks the item at the place as reviewed, only when it is the next one of the sample.</summary>
    internal async Task<BatchReviewOutcome> MarkAsync(BatchEntry batch, int place, CancellationToken cancellationToken) =>
        await TurnedBackAsync(batch, progress => progress.Mark(place), cancellationToken).ConfigureAwait(false)
            ?? (await steps.Marks.MarkAsync(new BatchMarking(batch.DomainId, batch.Id, place), cancellationToken).ConfigureAwait(false)).Outcome;

    /// <summary>Rejects the whole batch for the kind of personal data found at the place, which must be the next item of the sample.</summary>
    internal async Task<BatchReviewOutcome> RejectAsync(BatchEntry batch, (int Place, string Kind, long Reviewer) found, CancellationToken cancellationToken) =>
        await TurnedBackAsync(batch, progress => progress.Find(found.Place), cancellationToken).ConfigureAwait(false)
            ?? (await steps.Reviews.ReviewAsync(new BatchReviewing(batch.DomainId, batch.Id, found.Reviewer, found.Kind), cancellationToken).ConfigureAwait(false)).Outcome;

    /// <summary>Approves the batch, only when every item of the sample was reviewed.</summary>
    internal async Task<BatchReviewOutcome> ApproveAsync(BatchEntry batch, long reviewer, CancellationToken cancellationToken) =>
        await TurnedBackAsync(batch, progress => progress.Approve(), cancellationToken).ConfigureAwait(false)
            ?? (await steps.Approvals.ApproveAsync(new BatchApproval(batch.DomainId, batch.Id, reviewer), cancellationToken).ConfigureAwait(false)).Outcome;

    /// <summary>
    /// The sample for the screen: the items with their text, in the order of the draw, and who decided and when once the batch is decided. The outcome says not sampled when no draw was made,
    /// and not recorded when a text could not be read or recorded, in which case nothing of the sample is shown.
    /// </summary>
    internal async Task<(BatchReviewOutcome Outcome, SampleReviewScreen.Drawn? Screen)> ViewAsync(BatchEntry batch, string domain, long reviewer, CancellationToken cancellationToken)
    {
        var held = await steps.Reviews.ReadAsync(batch.DomainId, batch.Id, cancellationToken).ConfigureAwait(false);
        if (held?.Sample is null)
        {
            return (BatchReviewOutcome.NotSampled, null);
        }

        List<SampleReviewScreen.Item>? items;
        if (held.State == BatchState.Open)
        {
            using var access = await around.Texts(cancellationToken).ConfigureAwait(false);
            items = access.Reader is { } reader ? await SampleItems.OpenAsync(reader, (batch.Id, batch.DomainId, reviewer), held, cancellationToken).ConfigureAwait(false) : null;
        }
        else
        {
            items = SampleItems.Closed(held);
        }

        return items is null ? (BatchReviewOutcome.NotRecorded, null) : (BatchReviewOutcome.Drawn, await ScreenAsync(batch.Id, (domain, held, items), cancellationToken).ConfigureAwait(false));
    }

    private static BatchReviewOutcome Refused(SampleDecision decision) =>
        decision == SampleDecision.ReviewIncomplete ? BatchReviewOutcome.ReviewIncomplete : BatchReviewOutcome.PlaceNotNext;

    /// <summary>The outcome that turns a step back before any write: not sampled when no draw was made, or the reason the domain refuses the step; none when the step may go on.</summary>
    private async Task<BatchReviewOutcome?> TurnedBackAsync(BatchEntry batch, Func<SampleProgress, SampleDecision> decide, CancellationToken cancellationToken)
    {
        if (await steps.Reviews.ReadAsync(batch.DomainId, batch.Id, cancellationToken).ConfigureAwait(false) is not { Sample: { } sample } held)
        {
            return BatchReviewOutcome.NotSampled;
        }

        var decision = decide(new SampleProgress(sample.Count, held.ReviewedPosition));
        return decision == SampleDecision.Allowed ? null : Refused(decision);
    }

    private async Task<SampleReviewScreen.Drawn> ScreenAsync(long batch, (string Domain, BatchReview Held, List<SampleReviewScreen.Item> Items) shown, CancellationToken cancellationToken)
    {
        var held = shown.Held;
        var names = await around.Registry.NamesOfAsync(held.ReviewerId is { } reviewer ? [reviewer] : [], cancellationToken).ConfigureAwait(false);
        var by = held.ReviewerId is { } id && names.TryGetValue(id, out var name) ? name : Removed;
        var draw = "dos itens " + string.Join(", ", held.Sample!.Select(item => item.ToString(CultureInfo.InvariantCulture)));
        var approval = held.State == BatchState.Accepted && held.ReviewedAt is { } approvedAt ? new SampleReviewScreen.Approval(by, approvedAt, draw) : null;
        var rejection = held.Finding is { } kind && held.ReviewedAt is { } rejectedAt ? new SampleReviewScreen.Rejection(by, rejectedAt, held.FindingPosition ?? 0, kind) : null;
        return new SampleReviewScreen.Drawn(batch, shown.Domain, around.Declared ?? held.Sample!.Count, shown.Items, approval) { Rejected = rejection };
    }
}
