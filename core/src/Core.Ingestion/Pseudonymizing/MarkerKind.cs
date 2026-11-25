namespace Core.Ingestion.Pseudonymizing;

/// <summary>What a marker stands for, which the marker itself says, so that a reader of the pseudonymized tells a person, a place and an organization apart.</summary>
public enum MarkerKind
{
    /// <summary>A person.</summary>
    Person = 0,

    /// <summary>A place, by its proper name (decision 0056).</summary>
    Place = 1,

    /// <summary>An organization.</summary>
    Organization = 2,
}
