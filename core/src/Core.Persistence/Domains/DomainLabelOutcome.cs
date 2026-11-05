namespace Core.Persistence.Domains;

/// <summary>How an attempt to rotate or to revoke the label of a domain ended; every value but <see cref="Rotated" /> and <see cref="Revoked" /> is a refusal that wrote nothing.</summary>
public enum DomainLabelOutcome
{
    /// <summary>The labels of the next generation and the event of the rotation are written.</summary>
    Rotated,

    /// <summary>The revocation of the label and its event are written.</summary>
    Revoked,

    /// <summary>No domain has the identifier, or none with labels, which is the same since the creation writes both.</summary>
    DomainNotFound,

    /// <summary>The custody does not hold the labels of the next generation; the result names them.</summary>
    LabelsMissing,

    /// <summary>The text is not a derivation label, which is read strictly.</summary>
    LabelInvalid,

    /// <summary>No label registered has the text, so nothing was ever encrypted under it.</summary>
    LabelUnknown,

    /// <summary>The label was revoked already.</summary>
    AlreadyRevoked,

    /// <summary>The label is the newest generation not revoked of its purpose in its domain, and revoking it would leave the domain with nothing to encrypt under; rotate first.</summary>
    LastGeneration,

    /// <summary>Another rotation, or another writer of the chain, reached the same place first; nothing was written, and whoever asked may ask again.</summary>
    Conflict,

    /// <summary>The chain refused the event, so the change was not kept either.</summary>
    ChainRefused,
}
