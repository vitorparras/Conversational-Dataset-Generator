namespace Core.Boundary.Http;

/// <summary>What came of sending a seal: it arrived, or why it did not, in terms that carry nothing of the seal.</summary>
/// <param name="Failure">Why the seal did not arrive; <see cref="SealDeliveryFailure.None" /> when it did.</param>
/// <param name="Status">The status the destination answered with; 0 when it did not answer.</param>
public sealed record SealDeliveryResult(SealDeliveryFailure Failure, int Status)
{
    /// <summary>The seal arrived.</summary>
    public static SealDeliveryResult Arrived { get; } = new(SealDeliveryFailure.None, 0);

    /// <summary>Whether the seal arrived.</summary>
    public bool Delivered => Failure == SealDeliveryFailure.None;

    /// <summary>The seal did not arrive, for the reason given, and the destination did not answer.</summary>
    /// <param name="failure">The reason.</param>
    public static SealDeliveryResult Failed(SealDeliveryFailure failure) => new(failure, 0);

    /// <summary>The seal did not arrive, for the reason given, and the destination answered.</summary>
    /// <param name="failure">The reason.</param>
    /// <param name="status">The status the destination answered with.</param>
    public static SealDeliveryResult Failed(SealDeliveryFailure failure, int status) => new(failure, status);
}
