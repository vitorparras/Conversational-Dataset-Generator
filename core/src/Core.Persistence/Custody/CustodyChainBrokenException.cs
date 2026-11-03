namespace Core.Persistence.Custody;

/// <summary>
/// Content was not written because the last verification of the chain of custody failed; the chain is one for the installation, so the refusal is too.
/// Nothing was written, and the message carries nothing of the content.
/// </summary>
public sealed class CustodyChainBrokenException : InvalidOperationException
{
    /// <summary>The refusal, with the words of whoever operates.</summary>
    public CustodyChainBrokenException()
        : base("A última verificação da cadeia de custódia falhou: nenhum conteúdo é gravado até a cadeia ser restaurada; nada foi gravado.")
    {
    }

    /// <summary>The refusal, with the words given.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    public CustodyChainBrokenException(string? message)
        : base(message)
    {
    }

    /// <summary>The refusal, with the failure that brought it.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    /// <param name="innerException">The failure.</param>
    public CustodyChainBrokenException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
