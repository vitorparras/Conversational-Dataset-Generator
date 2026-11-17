namespace Core.Ingestion.Review;

/// <summary>What the review of a sample allows for a request, before anything is written.</summary>
public enum SampleDecision
{
    /// <summary>The request fits where the review stands.</summary>
    Allowed,

    /// <summary>The item is not the one right after the last reviewed, so that no item is skipped and none is marked twice.</summary>
    ItemIsNotNext,

    /// <summary>The place is not one of the sample.</summary>
    ItemOutsideTheSample,

    /// <summary>The sample has no item to review or one of them is not reviewed yet, and a batch is approved only with every item reviewed.</summary>
    ReviewIncomplete,
}
