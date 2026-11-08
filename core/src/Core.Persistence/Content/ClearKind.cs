namespace Core.Persistence.Content;

/// <summary>
/// What a column left in clear holds. The only free text is the pseudonymized one; the state and the motive of a batch are words of a closed form
/// that the table checks, and any other text is protected.
/// </summary>
public enum ClearKind
{
    /// <summary>The identifier of a row or of the row it points to.</summary>
    Identifier = 0,

    /// <summary>The domain the row belongs to.</summary>
    Domain = 1,

    /// <summary>A date or an instant.</summary>
    Moment = 2,

    /// <summary>The pseudonymized text, the one text that decision 0030 leaves in clear.</summary>
    PseudonymizedText = 3,

    /// <summary>The number of the epoch whose secret wraps the key of a row (decision 0049).</summary>
    Epoch = 4,

    /// <summary>The state a batch of ingestion is in: one of three words the table allows.</summary>
    Status = 5,

    /// <summary>The motive of a rejection, a name of lowercase letters, digits and underscores that the table checks, never free text.</summary>
    Motive = 6,

    /// <summary>The number of the transaction that wrote a row, which the row policy compares with the transaction that reads.</summary>
    Transaction = 7,

    /// <summary>The form an item was accommodated into, one of the four, or nothing when the item fits its form exactly.</summary>
    Form = 8,

    /// <summary>The codes of what the form received does not keep of an item, none when it fits exactly: codes of a closed set, never a word of the material.</summary>
    Losses = 9,
}
