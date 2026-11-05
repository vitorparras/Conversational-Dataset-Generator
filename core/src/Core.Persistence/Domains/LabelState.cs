namespace Core.Persistence.Domains;

/// <summary>What the database knows of a label when its revocation is asked for.</summary>
/// <param name="Known">Whether the label is registered.</param>
/// <param name="Revoked">Whether the label was revoked already.</param>
/// <param name="NewerKept">Whether a newer generation of the same purpose in the same domain is left that is not revoked.</param>
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
