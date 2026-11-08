namespace Core.Persistence.Content.Writing;

/// <summary>Content was added to a batch that is not open, or to one that does not exist in the domain: the batch takes content only while it is open. Nothing was written.</summary>
public sealed class BatchNotOpenException : InvalidOperationException
{
    /// <summary>The refusal, with the words of whoever operates.</summary>
    public BatchNotOpenException()
        : base("O lote não está aberto: nenhum conteúdo é gravado nele; nada foi gravado.")
    {
    }

    /// <summary>The refusal, with the words given.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    public BatchNotOpenException(string? message)
        : base(message)
    {
    }

    /// <summary>The refusal, with the failure that brought it.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    /// <param name="innerException">The failure.</param>
    public BatchNotOpenException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
