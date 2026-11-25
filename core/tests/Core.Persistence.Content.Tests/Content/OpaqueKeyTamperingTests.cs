using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>Criterion 4 of requirement 003 for the keys: each wrapped key column written in clear fails the detector, named with the ones written before it, and is found in the files.</summary>
[Trait("Category", "Database")]
public sealed class OpaqueKeyTamperingTests
{
    [DatabaseFact]
    public Task Ca4KeyColumnsThatWriteInClearFailTheDetectorAndAreNamedTogether() =>
        OpaqueTampering.TamperOneAfterAnotherAsync(OpaqueStorage.KeyColumnsWrapped, material => material.Keys.Keys, Range.All);
}
