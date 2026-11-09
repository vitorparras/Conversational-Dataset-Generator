namespace Core.Ingestion.Deciding;

/// <summary>What becomes of an occurrence the language service found; these three, and no other.</summary>
public enum Destination
{
    /// <summary>The span goes out of the text, because erasing it takes away the identity and nothing else.</summary>
    Remove = 0,

    /// <summary>The span becomes a stable marker, because erasing it would also undo a relation, such as the same person in two places or who answered whom.</summary>
    Pseudonymize = 1,

    /// <summary>The span stays as it is, because it is information the analysis needs, such as the place where something happened.</summary>
    Preserve = 2,
}
