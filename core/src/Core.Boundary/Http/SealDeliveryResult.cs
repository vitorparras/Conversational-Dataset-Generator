namespace Core.Boundary.Http;

/// <summary>What came of sending a seal: it arrived, or why it did not, in words that carry nothing of the seal.</summary>
/// <param name="Delivered">Whether the destination answered with a success.</param>
/// <param name="Reason">Why it did not arrive; empty when it did.</param>
public sealed record SealDeliveryResult(bool Delivered, string Reason)
{
    /// <summary>The seal arrived.</summary>
    public static SealDeliveryResult Arrived { get; } = new(true, string.Empty);

    /// <summary>The seal did not arrive, for the reason given.</summary>
    /// <param name="reason">The reason, which never carries the seal.</param>
    public static SealDeliveryResult Failed(string reason) => new(false, reason);
}
