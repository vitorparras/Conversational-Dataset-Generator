namespace Core.Persistence.Content;

/// <summary>What a column left in clear holds. The only kind of text is the pseudonymized one; any other text is protected.</summary>
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
}
