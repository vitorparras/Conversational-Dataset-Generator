namespace Core.Ingestion;

/// <summary>What a reader gives back: the material read whole, or the reason it could not; there is no third way, and no partial reading.</summary>
public sealed record ReadOutcome
{
    private ReadOutcome(ReadMaterial? material, UnreadableReason? reason)
    {
        Material = material;
        Reason = reason;
    }

    /// <summary>The material, read whole; null when it was not.</summary>
    public ReadMaterial? Material { get; }

    /// <summary>Why it could not be read; null when it was read.</summary>
    public UnreadableReason? Reason { get; }

    /// <summary>The material was read whole.</summary>
    public static ReadOutcome Read(ReadMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new(material, null);
    }

    /// <summary>The material could not be read, for the reason given; a value outside the enumeration is a defect.</summary>
    public static ReadOutcome Unreadable(UnreadableReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The reason is not one of the enumeration.");
        }

        return new(null, reason);
    }
}
