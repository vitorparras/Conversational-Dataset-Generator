namespace Core.Persistence.Ingestion;

/// <summary>What became of a request about the review of a batch.</summary>
public enum BatchReviewOutcome
{
    /// <summary>The sample was drawn and written, and the draw is in the chain.</summary>
    Drawn,

    /// <summary>The review found nothing and is in the chain: the batch can be accepted.</summary>
    Reviewed,

    /// <summary>The review found personal data not treated: the whole batch was rejected, its material removed, and the review stays in the chain.</summary>
    Rejected,

    /// <summary>The event could not be recorded, and nothing changed; the refusal says why.</summary>
    NotRecorded,

    /// <summary>The domain has no such batch.</summary>
    NotFound,

    /// <summary>The batch is not open: a closed batch is neither sampled nor reviewed.</summary>
    NotOpen,

    /// <summary>The sample of the batch was drawn before, and it is drawn once.</summary>
    AlreadySampled,

    /// <summary>The sample of the batch was not drawn yet, and a review is of a sample.</summary>
    NotSampled,

    /// <summary>The sample was reviewed before, and it is reviewed once.</summary>
    AlreadyReviewed,

    /// <summary>The finding is not a name of lowercase letters, digits and underscores.</summary>
    FindingRefused,

    /// <summary>The installation declares no size for the sample, and nothing is drawn without it.</summary>
    SizeNotDeclared,
}

