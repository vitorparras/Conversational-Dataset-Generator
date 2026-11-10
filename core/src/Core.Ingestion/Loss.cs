namespace Core.Ingestion;

/// <summary>
/// What the form a material was accommodated into does not keep of it, from a closed set: the choice of who submits is a code, and the text that describes the loss comes from the code
/// (<see cref="LossCodes.Words"/>), so that no free text, which could carry a word of the material, is ever kept beside an item.
/// </summary>
public enum Loss
{
    /// <summary>The author of each unit, which the form received has no place for.</summary>
    Author = 0,

    /// <summary>The instant of each unit and, with it, the temporal order.</summary>
    Instant = 1,

    /// <summary>Which unit answered which: the structure of replies.</summary>
    RepliesTo = 2,

    /// <summary>The passages that carried a label inside the text of a unit.</summary>
    Passages = 3,

    /// <summary>The thread: several units in sequence read as one manifestation.</summary>
    Thread = 4,
}
