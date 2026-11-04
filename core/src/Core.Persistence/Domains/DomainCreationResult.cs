using Core.Persistence.Custody;
using Core.Platform.Keys;

namespace Core.Persistence.Domains;

/// <summary>The answer of an attempt to create a domain: how it ended, the identifier the domain has or would have, and what is lacking when the labels are.</summary>
public sealed record DomainCreationResult
{
    /// <summary>How the attempt ended.</summary>
    public required DomainCreationOutcome Outcome { get; init; }

    /// <summary>The identifier the domain took, or, when the labels are missing, the one it would take; null when the attempt never reached the registry.</summary>
    public int? DomainId { get; init; }

    /// <summary>The derivation labels the custody lacks, in order; empty unless <see cref="Outcome" /> is <see cref="DomainCreationOutcome.LabelsMissing" />.</summary>
    public IReadOnlyList<string> MissingLabels { get; init; } = DerivationLabels.None.All;

    /// <summary>The sequence of the event in the chain when the domain was created.</summary>
    public long? Sequence { get; init; }

    /// <summary>How the chain ended when it refused the event; null otherwise.</summary>
    public CustodyOutcome? ChainOutcome { get; init; }
}
