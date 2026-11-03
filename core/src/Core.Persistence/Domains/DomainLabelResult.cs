using Core.Persistence.Custody;
using Core.Platform.Keys;

namespace Core.Persistence.Domains;

/// <summary>The answer of an attempt to rotate or to revoke the label of a domain: how it ended, the domain and the generation concerned, and what is lacking when the labels are.</summary>
public sealed record DomainLabelResult
{
    /// <summary>How the attempt ended.</summary>
    public required DomainLabelOutcome Outcome { get; init; }

    /// <summary>The domain concerned; null when the attempt never reached one.</summary>
    public int? DomainId { get; init; }

    /// <summary>The generation the domain writes under after a rotation, or would have, or the one of the label revoked.</summary>
    public int? Version { get; init; }

    /// <summary>The derivation labels the custody lacks, in order; empty unless <see cref="Outcome" /> is <see cref="DomainLabelOutcome.LabelsMissing" />.</summary>
    public IReadOnlyList<string> MissingLabels { get; init; } = DerivationLabels.None.All;

    /// <summary>The sequence of the event in the chain when the change was written.</summary>
    public long? Sequence { get; init; }

    /// <summary>How the chain ended when it refused the event; null otherwise.</summary>
    public CustodyOutcome? ChainOutcome { get; init; }
}
