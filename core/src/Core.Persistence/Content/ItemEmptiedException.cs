namespace Core.Persistence.Content;

/// <summary>A map was added to an item that was emptied: what it would hold is what the emptying took away. Nothing was written, and the message carries nothing of the content.</summary>
public sealed class ItemEmptiedException : InvalidOperationException
{
    /// <summary>The refusal, with the words of whoever operates.</summary>
    public ItemEmptiedException()
        : base("O item foi esvaziado: nenhum mapa é gravado para ele; nada foi gravado.")
    {
    }

    /// <summary>The refusal, with the words given.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    public ItemEmptiedException(string? message)
        : base(message)
    {
    }

    /// <summary>The refusal, with the failure that brought it.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    /// <param name="innerException">The failure.</param>
    public ItemEmptiedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
