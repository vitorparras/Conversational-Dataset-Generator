namespace Core.Persistence.Domains;

/// <summary>How an attempt to create a domain ended; every value but <see cref="Created" /> is a refusal that wrote nothing.</summary>
public enum DomainCreationOutcome
{
    /// <summary>The domain, its two derivation labels and the event of its creation are written.</summary>
    Created,

    /// <summary>The name is empty or has a space around it.</summary>
    NameInvalid,

    /// <summary>A domain of that name exists already.</summary>
    NameTaken,

    /// <summary>The custody does not hold the derivation labels of the two purposes for the identifier the domain would take; the result names them.</summary>
    LabelsMissing,

    /// <summary>Another creation, or another writer of the chain, reached the same place first; nothing was written, and whoever asked may ask again.</summary>
    Conflict,

    /// <summary>The chain refused the event of the creation, so the domain was not kept either; the result carries how the chain ended.</summary>
    ChainRefused,
}
