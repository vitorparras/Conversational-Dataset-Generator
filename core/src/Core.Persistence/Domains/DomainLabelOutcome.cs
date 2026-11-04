namespace Core.Persistence.Domains;

/// <summary>How an attempt to rotate the label of a domain ended; every value but <see cref="Rotated" /> is a refusal that wrote nothing.</summary>
public enum DomainLabelOutcome
{
    /// <summary>The labels of the next generation and the event of the rotation are written.</summary>
    Rotated,

    /// <summary>No domain has the identifier, or none with labels, which is the same since the creation writes both.</summary>
    DomainNotFound,

    /// <summary>The custody does not hold the labels of the next generation; the result names them.</summary>
    LabelsMissing,

    /// <summary>Another rotation, or another writer of the chain, reached the same place first; nothing was written, and whoever asked may ask again.</summary>
    Conflict,

    /// <summary>The chain refused the event, so the change was not kept either.</summary>
    ChainRefused,
}
