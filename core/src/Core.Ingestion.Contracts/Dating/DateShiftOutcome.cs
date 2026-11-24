namespace Core.Ingestion.Contracts.Dating;

/// <summary>What establishing the shift of a domain comes to.</summary>
public enum DateShiftOutcome
{
    /// <summary>The domain held no shift, and one was drawn, which counts only once it is kept.</summary>
    Drawn,

    /// <summary>The domain holds a shift, and it opened.</summary>
    Kept,

    /// <summary>The domain holds a shift that does not open: it is lost for good, and nothing is drawn over it.</summary>
    Lost,
}
