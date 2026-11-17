namespace Core.Ingestion.Review;

/// <summary>Where the review of a drawn sample stands (#610): places go from 1 to the size, in the order of the draw, each reviewed once right after the one before; it only decides.</summary>
/// <param name="Size">How many items the sample holds.</param>
/// <param name="Reviewed">How many of them, from the first, were already marked as reviewed.</param>
public sealed record SampleProgress(int Size, int Reviewed)
{
    /// <summary>The place that can be marked now, or null when every place was reviewed.</summary>
    public int? Next => Reviewed < Size ? Reviewed + 1 : null;

    /// <summary>Whether a place of the sample is marked as reviewed.</summary>
    public SampleDecision Mark(int position) => PlaceDecision(position);

    /// <summary>
    /// Whether a finding of personal data can be told at a place: at the one being reviewed or at one already marked, since one found late still rejects the batch; never at one not reached.
    /// </summary>
    public SampleDecision Find(int position) =>
        position >= 1 && position <= Math.Min(Size, Reviewed + 1) ? SampleDecision.Allowed : PlaceDecision(position);

    /// <summary>Whether the batch can be approved: only when the sample has items and every one was reviewed.</summary>
    public SampleDecision Approve() => Size > 0 && Reviewed == Size ? SampleDecision.Allowed : SampleDecision.ReviewIncomplete;

    private SampleDecision PlaceDecision(int position)
    {
        if (position < 1 || position > Size)
        {
            return SampleDecision.ItemOutsideTheSample;
        }

        return position == Next ? SampleDecision.Allowed : SampleDecision.ItemIsNotNext;
    }
}
