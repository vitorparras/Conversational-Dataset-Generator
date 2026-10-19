using Core.Tests.Properties.Harness;
using CsCheck;

namespace Core.Tests.Properties.Boundary;

/// <summary>The conversion keeps the invariants of decisão 0011 and requisito 009; a conversion that breaks one falls to its property.</summary>
public sealed class OffsetInvariantsTests
{
    private static OffsetInvariants Over(OffsetConversion conversion) => new(PropertyRunner.FromProcess(), conversion);

    [Fact]
    [Trait("Kind", "Property")]
    public void CodePointOffsetSurvivesTheRoundTrip() => Over(OffsetConversion.Production).CodePointOffsetSurvivesTheRoundTrip();

    [Fact]
    [Trait("Kind", "Property")]
    public void SliceIsTheSpanTheServiceAnalysed() => Over(OffsetConversion.Production).SliceIsTheSpanTheServiceAnalysed();

    [Fact]
    [Trait("Kind", "Property")]
    public void IndexInsideAPairIsRefused() => Over(OffsetConversion.Production).IndexInsideAPairIsRefused();

    [Fact]
    public void AConversionCountingUtf16UnitsFailsTheRoundTrip() =>
        Assert.Throws<CsCheckException>(Over(OffsetConversion.CountingUtf16Units).CodePointOffsetSurvivesTheRoundTrip);

    [Fact]
    public void ASliceAtTheRawOffsetsFailsTheSpanProperty() =>
        Assert.Throws<CsCheckException>(Over(OffsetConversion.SlicingRawOffsets).SliceIsTheSpanTheServiceAnalysed);

    [Fact]
    public void AConversionRoundingInsidePairsFailsTheRefusalProperty() =>
        Assert.Throws<CsCheckException>(Over(OffsetConversion.RoundingInsidePairs).IndexInsideAPairIsRefused);
}
