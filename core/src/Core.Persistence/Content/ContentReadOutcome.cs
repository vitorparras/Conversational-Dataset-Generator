namespace Core.Persistence.Content;

/// <summary>How a reading of protected content ended; the text exists only when it was read.</summary>
public enum ContentReadOutcome
{
    /// <summary>The event of the reading is in the chain and the content was opened.</summary>
    Read,

    /// <summary>The domain holds no such item, or the domain declared is not its own; nothing was read, and nothing is recorded.</summary>
    NotFound,

    /// <summary>The item exists and the column holds nothing, because the stage that makes it has not run or the item was emptied; nothing was read.</summary>
    Empty,

    /// <summary>The chain did not accept the event of the reading, so the content was not opened.</summary>
    NotRecorded,
}
