namespace Core.Ingestion.Sanitizing;

/// <summary>Either the sanitized text, or the reason the material was not sanitized; never both, never neither.</summary>
public sealed record SanitizationResult
{
    private SanitizationResult(SanitizedText? sanitized, SanitizationRefusal? refusal)
    {
        Sanitized = sanitized;
        Refusal = refusal;
    }

    /// <summary>The sanitized text with its trail and report; null when the material was refused.</summary>
    public SanitizedText? Sanitized { get; }

    /// <summary>Why the material was not sanitized; null when it was.</summary>
    public SanitizationRefusal? Refusal { get; }

    /// <summary>The material went through every step.</summary>
    internal static SanitizationResult Of(SanitizedText sanitized)
    {
        ArgumentNullException.ThrowIfNull(sanitized);
        return new(sanitized, null);
    }

    /// <summary>The material was not sanitized, for the reason given, and no text stands in its place.</summary>
    /// <param name="refusal">Why the material was not sanitized.</param>
    public static SanitizationResult Refused(SanitizationRefusal refusal) => new(null, refusal);
}
