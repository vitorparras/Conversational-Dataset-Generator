namespace Core.Boundary.Contracts;

/// <summary>Either the analysis, possibly empty, or the reason there is none; never both, never neither.</summary>
public sealed record RecognitionOutcome
{
    private RecognitionOutcome(AnalysisResponse? response, UnavailabilityReason? reason)
    {
        Response = response;
        Reason = reason;
    }

    /// <summary>The analysis; null when the service was unavailable.</summary>
    public AnalysisResponse? Response { get; }

    /// <summary>Why there is no analysis; null when the text was analysed.</summary>
    public UnavailabilityReason? Reason { get; }

    /// <summary>The text was analysed; an empty list of occurrences is still an analysis.</summary>
    public static RecognitionOutcome Analyzed(AnalysisResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new(response, null);
    }

    /// <summary>The text was not analysed, for the reason given; a value outside the enumeration is a defect.</summary>
    public static RecognitionOutcome Unavailable(UnavailabilityReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The reason is not one of the enumeration.");
        }

        return new(null, reason);
    }
}
