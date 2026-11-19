using Core.Boundary.Contracts;

namespace Core.Ingestion.Sanitizing;

/// <summary>The type written in place of what was removed; the marker says what it was, never a character of it.</summary>
public enum SanitizationMarker
{
    /// <summary>A secret, by the list of <see cref="OccurrenceKinds"/>; written as [credencial].</summary>
    Credential = 0,

    /// <summary>Any other occurrence the policy removes; written as [dado].</summary>
    Data = 1,
}
