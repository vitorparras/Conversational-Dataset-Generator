namespace Core.Ingestion.Contracts;

/// <summary>What the state of a batch allows of the review of its sample (issue 83): one decision, with no read and no write, that the database and the domain take alike.</summary>
public static class SampleStateRules
{
    /// <summary>Why the batch refuses the step, or null when it allows it.</summary>
    /// <param name="batch">The batch as it is held, or null when it does not exist.</param>
    /// <param name="drawing">True for the draw of the sample, false for a step that reviews it.</param>
    public static SampleRefusal? Refusal(SampledBatch? batch, bool drawing)
    {
        if (batch is not { } held)
        {
            return SampleRefusal.NotFound;
        }

        if (!held.Open && (drawing || !held.Reviewed))
        {
            return SampleRefusal.NotOpen;
        }

        if (drawing)
        {
            return held.Sampled ? SampleRefusal.AlreadySampled : null;
        }

        if (!held.Sampled)
        {
            return SampleRefusal.NotSampled;
        }

        return held.Reviewed ? SampleRefusal.AlreadyReviewed : null;
    }
}
