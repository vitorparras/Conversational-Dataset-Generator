using Core.Persistence.Custody;

namespace Core.Persistence.Settings;

/// <summary>The answer of a submission of a change of configuration: what the chain answered and, when it was recorded, the identifier the submission took.</summary>
public sealed record ConfigurationChangeResult
{
    /// <summary>What the chain answered; <see cref="CustodyOutcome.Appended" /> when the submission and its event were written.</summary>
    public required CustodyResult Recorded { get; init; }

    /// <summary>The submission written; null when nothing was.</summary>
    public long? ChangeId { get; init; }
}
