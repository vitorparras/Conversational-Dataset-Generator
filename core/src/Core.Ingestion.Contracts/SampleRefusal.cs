namespace Core.Ingestion.Contracts;

/// <summary>Why the state of a batch refuses a step of the review of its sample, before anything is read of the sample itself.</summary>
public enum SampleRefusal
{
    /// <summary>The batch does not exist.</summary>
    NotFound,

    /// <summary>The batch no longer waits for its acceptance or its rejection and the step is not a reading of the one already reviewed.</summary>
    NotOpen,

    /// <summary>The sample of the batch was already drawn, and a second draw would replace it.</summary>
    AlreadySampled,

    /// <summary>No sample was drawn for the batch, so there is nothing to review.</summary>
    NotSampled,

    /// <summary>The sample of the batch was already reviewed.</summary>
    AlreadyReviewed,
}
