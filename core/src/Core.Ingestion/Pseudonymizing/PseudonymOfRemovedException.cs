namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// A span whose destination is not to pseudonymize reached the swap: a credential or a document number in the map would make the map a file of them, so the swap stops before
/// anything is sealed. It is a defect upstream, since only the occurrences to pseudonymize are given to the swap.
/// </summary>
public sealed class PseudonymOfRemovedException : InvalidOperationException
{
    /// <summary>The defect, with no word of the span.</summary>
    public PseudonymOfRemovedException()
        : base("A span whose destination is not to pseudonymize never becomes an entry of the map.")
    {
    }

    /// <summary>The defect, with the message given.</summary>
    /// <param name="message">What happened, with no word of the span.</param>
    public PseudonymOfRemovedException(string? message)
        : base(message)
    {
    }

    /// <summary>The defect, with the message and the cause given.</summary>
    /// <param name="message">What happened, with no word of the span.</param>
    /// <param name="innerException">What caused it.</param>
    public PseudonymOfRemovedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
