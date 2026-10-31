namespace Core.Platform.Keys;

/// <summary>An envelope was not opened: it is not an envelope of this format, it was sealed under another label, or its content or header was changed. It is never an empty content.</summary>
public sealed class EnvelopeRefusedException : Exception
{
    /// <summary>Refuses the envelope without a reason.</summary>
    public EnvelopeRefusedException()
    {
    }

    /// <summary>Refuses the envelope, saying why in words that carry neither the key nor the content.</summary>
    public EnvelopeRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>Refuses the envelope because the cipher did not authenticate it.</summary>
    public EnvelopeRefusedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
