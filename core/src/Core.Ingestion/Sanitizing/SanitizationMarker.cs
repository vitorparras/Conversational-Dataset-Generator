using Core.Boundary.Contracts;

namespace Core.Ingestion.Sanitizing;

/// <summary>The type written in place of what was removed; the marker says what it was, never a character of it.</summary>
public enum SanitizationMarker
{
    /// <summary>A secret, by the list of <see cref="OccurrenceKinds"/>; written as [credencial].</summary>
    Credential = 0,

    /// <summary>Any other occurrence the policy removes; written as [dado].</summary>
    Data = 1,

    /// <summary>A document number, a bank account, a plate or a device identifier, removed by its form; written as [documento].</summary>
    Document = 2,

    /// <summary>A telephone or an electronic address, removed by its form; written as [contato].</summary>
    Contact = 3,
}
