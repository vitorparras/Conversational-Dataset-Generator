using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The event of a recovery rehearsed and checked (issue 67, part 2): the system's, about the master key,
/// with the top of the copy as the id, for an administrative act and with the digest of the receipt as the reference.
/// </summary>
public sealed class RecoveryTestedEventTests
{
    [Fact]
    public void TheEventIsTheSystemsAboutTheMasterKeyWithTheTopOfTheCopyAsTheIdAndTheDigestOfTheReceiptAsTheReference()
    {
        var digest = new string('a', 64);

        var tested = CustodyEvent.RecoveryRehearsed(42, digest);

        Assert.Equal(
            (CustodyStep.RecoveryTested, CustodyItemKind.MasterKey, 42L, CustodyReason.AdministrativeAct, CustodyAuthor.System, digest),
            (tested.Step, tested.Item.Kind, tested.Item.Id, tested.Reason, tested.Author, tested.Reference));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATopThatIsNotPositiveIsADefectOfTheCaller(long top) =>
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => CustodyEvent.RecoveryRehearsed(top, new string('a', 64)));

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    public void ADigestThatIsNotAReferenceIsADefectOfTheCaller(string digest) =>
        Assert.Throws<ArgumentException>(() => CustodyEvent.RecoveryRehearsed(1, digest));

    [Fact]
    public void TheStepHasItsNameInTheChainAndIsOneOfTheStepsThatHaveAFactoryOfTheirOwn()
    {
        Assert.Equal("recovery_tested", CustodyNames.Of(CustodyStep.RecoveryTested));
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.MasterKey, 1), CustodyStep.RecoveryTested, CustodyReason.AdministrativeAct));
    }
}
