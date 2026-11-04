namespace Core.Persistence.Custody.Verification;

/// <summary>The chain against the seal someone brought from outside the control of the database: how it stands, and the words for whoever operates. It carries no content of any event.</summary>
public sealed record SealComparison
{
    /// <summary>The seal the chain was compared with.</summary>
    public required ChainSeal Sealed { get; init; }

    /// <summary>How the chain stands against it.</summary>
    public required SealOutcome Outcome { get; init; }

    /// <summary>The standing in words, for whoever operates.</summary>
    public string Statement => SealWords.Of(Outcome);

    /// <summary>Whether the chain has the event sealed with the digest sealed.</summary>
    public bool Holds => Outcome == SealOutcome.Holds;
}
