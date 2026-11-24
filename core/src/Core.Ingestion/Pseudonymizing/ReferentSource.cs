namespace Core.Ingestion.Pseudonymizing;

/// <summary>Where the identity of a referent comes from.</summary>
public enum ReferentSource
{
    /// <summary>The author of messages, by the name the material gives.</summary>
    Author = 0,

    /// <summary>A group of mentions the coreference joined.</summary>
    Group = 1,
}
