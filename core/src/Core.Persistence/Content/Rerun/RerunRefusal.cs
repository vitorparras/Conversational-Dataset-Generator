namespace Core.Persistence.Content.Rerun;

/// <summary>Why the rerun of an item was refused.</summary>
public enum RerunRefusal
{
    /// <summary>The item was emptied: there is no raw text to rerun.</summary>
    Emptied,

    /// <summary>The loss of the key the raw text is sealed under was declared.</summary>
    KeyLost,

    /// <summary>The item spent every try.</summary>
    Failed,
}
