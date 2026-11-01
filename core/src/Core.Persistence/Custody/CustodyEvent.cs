namespace Core.Persistence.Custody;

/// <summary>One event to add to the chain: who, about which item, which step and which reason, and the earlier event it corrects when it is a correction.</summary>
public sealed record CustodyEvent
{
    private CustodyEvent(CustodyAuthor author, CustodyItem item, CustodyStep step, CustodyReason reason)
    {
        Author = author;
        Item = item;
        Step = step;
        Reason = reason;
    }

    /// <summary>Who did it.</summary>
    public CustodyAuthor Author { get; }

    /// <summary>What it was about.</summary>
    public CustodyItem Item { get; }

    /// <summary>The step recorded.</summary>
    public CustodyStep Step { get; }

    /// <summary>Why.</summary>
    public CustodyReason Reason { get; }

    /// <summary>The sequence of the event this one corrects; null when it corrects none.</summary>
    public long? Corrects { get; private init; }

    /// <summary>An event that records a step; the correction of an event has its own constructor.</summary>
    /// <param name="author">Who did it.</param>
    /// <param name="item">What it was about.</param>
    /// <param name="step">Any step but <see cref="CustodyStep.Correction" /> and <see cref="CustodyStep.VerifyChain" />.</param>
    /// <param name="reason">Why.</param>
    public static CustodyEvent Of(CustodyAuthor author, CustodyItem item, CustodyStep step, CustodyReason reason)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(item);
        return step is CustodyStep.Correction or CustodyStep.VerifyChain
            ? throw new ArgumentException("A correction names the event it corrects, and a verification is written by the system; use Correction or Verification.", nameof(step))
            : new CustodyEvent(author, item, step, reason);
    }

    /// <summary>
    /// The event that records the verdict of a verification of the chain, written by the system: the item is the chain, its id is the number of events the verification read,
    /// and, when the verification failed, the event it points at is the first inconsistent one.
    /// </summary>
    /// <param name="eventsRead">How many events the verification read.</param>
    /// <param name="firstInconsistent">The sequence of the first inconsistent event; null when the chain held.</param>
    public static CustodyEvent Verification(long eventsRead, long? firstInconsistent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventsRead);
        if (firstInconsistent is { } sequence)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        }

        var reason = firstInconsistent is null ? CustodyReason.VerificationPassed : CustodyReason.VerificationFailed;
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, eventsRead), CustodyStep.VerifyChain, reason) { Corrects = firstInconsistent };
    }

    /// <summary>A new event that corrects an earlier one, which stays in the sequence as it was.</summary>
    /// <param name="author">Who corrects.</param>
    /// <param name="item">What the corrected event was about.</param>
    /// <param name="corrected">The sequence of the event corrected.</param>
    public static CustodyEvent Correction(CustodyAuthor author, CustodyItem item, long corrected)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(corrected);
        return new CustodyEvent(author, item, CustodyStep.Correction, CustodyReason.CorrectionOfRecord) { Corrects = corrected };
    }
}
