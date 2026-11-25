namespace Core.Boundary.Contracts;

/// <summary>Either the analysis of the item, possibly empty, or the reason there is none; never both, never neither.</summary>
public sealed record ItemRecognitionOutcome
{
    private ItemRecognitionOutcome(ItemAnalysisResponse? response, UnavailabilityReason? reason)
    {
        Response = response;
        Reason = reason;
    }

    /// <summary>The analysis; null when the service was unavailable.</summary>
    public ItemAnalysisResponse? Response { get; }

    /// <summary>Why there is no analysis; null when the item was analysed.</summary>
    public UnavailabilityReason? Reason { get; }

    /// <summary>The item was analysed; an empty list of occurrences is still an analysis.</summary>
    public static ItemRecognitionOutcome Analyzed(ItemAnalysisResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new(response, null);
    }

    /// <summary>The item was not analysed, for the reason given; a value outside the enumeration is a defect.</summary>
    public static ItemRecognitionOutcome Unavailable(UnavailabilityReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The reason is not one of the enumeration.");
        }

        return new(null, reason);
    }
}
