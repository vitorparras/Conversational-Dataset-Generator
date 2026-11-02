namespace Core.Persistence.Content;

/// <summary>How a request to empty an item ended; only <see cref="Emptied" /> changes content.</summary>
public enum EmptyingOutcome
{
    /// <summary>The event is in the chain, and the item and its maps were emptied in the same transaction.</summary>
    Emptied,

    /// <summary>The domain holds no such item; nothing was emptied, and nothing is recorded.</summary>
    NotFound,

    /// <summary>The author does not hold the function of curation of the collection; the refusal is in the chain, and the content is as it was.</summary>
    AuthorWithoutFunction,

    /// <summary>The item had been emptied already; the refusal is in the chain, and nothing is emptied again.</summary>
    AlreadyEmptied,

    /// <summary>The reference is not a protocol number; nothing was done, and nothing is recorded.</summary>
    ReferenceRefused,

    /// <summary>The chain did not accept the event, so nothing was emptied; the answer carries what the chain said.</summary>
    NotRecorded,
}
