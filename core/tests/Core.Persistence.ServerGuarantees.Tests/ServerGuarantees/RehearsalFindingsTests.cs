using Core.Persistence.ServerGuarantees;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The closed list of what a rehearsal of a restore finds (issue 66): the bits of the guarantees are the ones of the conference, the others do not clash, and the names fit a reference.
/// </summary>
public sealed class RehearsalFindingsTests
{
    [Theory]
    [InlineData(RehearsalFindings.Privilege, GuaranteeKinds.Privilege)]
    [InlineData(RehearsalFindings.Policy, GuaranteeKinds.RowPolicy)]
    [InlineData(RehearsalFindings.Revoke, GuaranteeKinds.ColumnRevoke)]
    [InlineData(RehearsalFindings.Immutable, GuaranteeKinds.Immutability)]
    [InlineData(RehearsalFindings.Columns, GuaranteeKinds.ProtectedColumns)]
    public void EachGuaranteeKeepsTheBitItHasInTheConference(RehearsalFindings finding, GuaranteeKinds guarantee) =>
        Assert.Equal((long)guarantee, (long)finding);

    [Fact]
    public void TheSampleOfSealedRecordsIsNotAFindingBecauseOpeningItWouldReadContent() =>
        Assert.DoesNotContain(Enum.GetValues<RehearsalFindings>(), finding => ((long)finding & (long)GuaranteeKinds.EncryptedSample) != 0);

    [Fact]
    public void NothingFoundIsNamedHeld() =>
        Assert.Equal("held", RestoreRehearsal.NamesOf(RehearsalFindings.None));

    [Fact]
    public void WhatFailedIsNamedInTheOrderOfTheBits() =>
        Assert.Equal("policy.chain.seal", RestoreRehearsal.NamesOf(RehearsalFindings.Seal | RehearsalFindings.Chain | RehearsalFindings.Policy));

    [Fact]
    public void EverythingFailingStillFitsAReferenceOfSixtyFourCharacters()
    {
        var everything = Enum.GetValues<RehearsalFindings>().Aggregate(RehearsalFindings.None, (all, finding) => all | finding);

        Assert.InRange(RestoreRehearsal.NamesOf(everything).Length, 1, 64);
        Assert.InRange((long)everything, 1, 255);
    }
}
