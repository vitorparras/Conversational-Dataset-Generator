namespace Core.Persistence.Content.Writing;

/// <summary>The database refused a pseudonymized text that came without its map, or a batch with one accepted without the event of the pseudonymization.</summary>
public sealed class PseudonymizedRefusedException : InvalidOperationException
{
    /// <summary>The refusal, with the message that says what the database asks.</summary>
    public PseudonymizedRefusedException()
        : base("A pseudonymized text is written with its map, in the same transaction.")
    {
    }

    /// <summary>The refusal, with its message.</summary>
    /// <param name="message">What the database asks.</param>
    public PseudonymizedRefusedException(string? message)
        : base(message)
    {
    }

    /// <summary>The refusal, with its message and its cause.</summary>
    /// <param name="message">What the database asks.</param>
    /// <param name="innerException">The refusal of the database.</param>
    public PseudonymizedRefusedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
