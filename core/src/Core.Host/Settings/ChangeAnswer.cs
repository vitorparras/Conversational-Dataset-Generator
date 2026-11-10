using Core.Platform.Configuration;

namespace Core.Host.Settings;

/// <summary>What the desk answers a submission with: how it ended, the problems of a value refused, and the submission written.</summary>
internal sealed record ChangeAnswer
{
    /// <summary>How it ended.</summary>
    internal required ChangeOutcome Outcome { get; init; }

    /// <summary>What the start would refuse, each naming the key and the value found, for <see cref="ChangeOutcome.ValueRefused" />; null otherwise.</summary>
    internal IReadOnlyList<ConfigurationProblem>? Problems { get; init; }

    /// <summary>The submission written, for <see cref="ChangeOutcome.Recorded" />.</summary>
    internal long? ChangeId { get; init; }

    /// <summary>The sequence of its event in the chain, for <see cref="ChangeOutcome.Recorded" />.</summary>
    internal long? Sequence { get; init; }
}
