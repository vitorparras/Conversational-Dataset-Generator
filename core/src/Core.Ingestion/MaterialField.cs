namespace Core.Ingestion;

/// <summary>The part of a unit read that a finding of the conference points at.</summary>
public enum MaterialField
{
    /// <summary>The text of the unit.</summary>
    Text = 0,

    /// <summary>Who wrote the unit.</summary>
    Author = 1,

    /// <summary>The moment the unit carries in its own content.</summary>
    Instant = 2,

    /// <summary>The author the unit answers.</summary>
    RepliesTo = 3,

    /// <summary>The limits of the passages of the unit.</summary>
    Passages = 4,
}
