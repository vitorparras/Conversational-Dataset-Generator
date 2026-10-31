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
    /// <param name="step">Any step but <see cref="CustodyStep.Correction" />.</param>
    /// <param name="reason">Why.</param>
    public static CustodyEvent Of(CustodyAuthor author, CustodyItem item, CustodyStep step, CustodyReason reason)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(item);
        return step == CustodyStep.Correction
            ? throw new ArgumentException("A correction names the event it corrects; use Correction.", nameof(step))
            : new CustodyEvent(author, item, step, reason);
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
