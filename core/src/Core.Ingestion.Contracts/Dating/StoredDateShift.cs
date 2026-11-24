namespace Core.Ingestion.Contracts.Dating;

/// <summary>The row of the shift a domain holds, as the store found it: none, one that did not open, or one and the text it opened to.</summary>
/// <param name="Held">Whether the domain holds a row.</param>
/// <param name="Text">The text the row opened to, none when it did not open.</param>
public sealed record StoredDateShift(bool Held, string? Text)
{
    /// <summary>The domain holds no row.</summary>
    public static StoredDateShift None => new(false, null);

    /// <summary>The domain holds a row that did not open: its key or its label is not at hand, or it does not authenticate.</summary>
    public static StoredDateShift Unopened => new(true, null);

    /// <summary>The domain holds a row that opened to the text.</summary>
    public static StoredDateShift Opened(string text) => new(true, text);
}
