namespace Core.Persistence.Recovery;

/// <summary>What reading the text of a receipt gave: the receipt, or the reason in words for whoever operates.</summary>
public sealed record RecoveryReading
{
    /// <summary>The receipt the text holds; null when it was not read.</summary>
    public RecoveryReceipt? Receipt { get; init; }

    /// <summary>Why the text was not read; null when it was.</summary>
    public string? Refusal { get; init; }

    internal static RecoveryReading Of(RecoveryReceipt receipt) => new() { Receipt = receipt };

    internal static RecoveryReading Refused(string reason) => new() { Refusal = reason };
}
