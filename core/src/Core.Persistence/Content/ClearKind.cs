namespace Core.Persistence.Content;

/// <summary>
/// What a column left in clear holds. The only free text is the pseudonymized one; the state, the motive and the condition of a batch are words of a closed form
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

    /// <summary>A name of lowercase letters, digits and underscores that the table checks, never free text: the motive of a rejection and the condition of origin of a batch.</summary>
    Motive = 6,

    /// <summary>The number of the transaction that wrote a row, which the row policy compares with the transaction that reads.</summary>
    Transaction = 7,
}
