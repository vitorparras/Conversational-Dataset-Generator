namespace Core.Persistence.Custody.Verification;

/// <summary>A seal was to be recorded, but the chain has no event at the sequence it points at, or the digest of that event is not the seal's. Nothing was written.</summary>
public sealed class SealDoesNotMatchException : InvalidOperationException
{
    /// <summary>The refusal, with the words of whoever operates.</summary>
    public SealDoesNotMatchException()
        : base("O selo não é da cadeia: ela não tem o evento selado com o resumo que o selo traz; nada foi gravado.")
    {
    }

    /// <summary>The refusal, with the words given.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    public SealDoesNotMatchException(string? message)
        : base(message)
    {
    }

    /// <summary>The refusal, with the failure that brought it.</summary>
    /// <param name="message">What to tell whoever operates.</param>
    /// <param name="innerException">The failure.</param>
    public SealDoesNotMatchException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
