namespace Core.Ingestion;

/// <summary>What the conference of a form gives: the material with its form, or the refusal; never both, never neither.</summary>
public sealed record ConferenceResult
{
    private ConferenceResult(Material? accepted, Refusal? refusal)
    {
        Accepted = accepted;
        Refusal = refusal;
    }

    /// <summary>The material accepted, with the form it answers for; null when refused.</summary>
    public Material? Accepted { get; }

    /// <summary>The refusal; null when accepted.</summary>
    public Refusal? Refusal { get; }

    /// <summary>The material holds up to the form declared.</summary>
    public static ConferenceResult Accept(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new(material, null);
    }

    /// <summary>The material does not hold up to the form declared, and the refusal says where.</summary>
    public static ConferenceResult Refuse(Refusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new(null, refusal);
    }
}
