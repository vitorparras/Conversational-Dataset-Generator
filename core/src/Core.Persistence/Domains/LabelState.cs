namespace Core.Persistence.Domains;

/// <summary>What the database knows of a label when its revocation is asked for: registered, revoked already, and whether a newer generation of its purpose in its domain is left unrevoked.</summary>
internal sealed record LabelState(bool Known, bool Revoked, bool NewerKept)
{
    /// <summary>Why the revocation is refused; null when it may go on.</summary>
    internal DomainLabelOutcome? Refusal()
    {
        if (!Known)
        {
            return DomainLabelOutcome.LabelUnknown;
        }
        if (Revoked)
        {
            return DomainLabelOutcome.AlreadyRevoked;
        }
        return NewerKept ? null : DomainLabelOutcome.LastGeneration;
    }
}
