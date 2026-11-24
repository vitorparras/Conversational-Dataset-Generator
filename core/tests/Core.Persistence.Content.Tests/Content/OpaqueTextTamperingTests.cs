using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>Criterion 4 of requirement 003 for the texts: each protected text column written in clear fails the detector, named with the ones written before it.</summary>
[Trait("Category", "Database")]
public sealed class OpaqueTextTamperingTests
{
    [DatabaseFact]
    public Task Ca4TextColumnsThatWriteInClearFailTheDetectorAndAreNamedTogether() =>
        OpaqueTampering.TamperOneAfterAnotherAsync(OpaqueStorage.TextColumns, material => material.Texts.Keys);
}
