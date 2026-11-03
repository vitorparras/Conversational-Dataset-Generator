using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The event of a recovery rehearsed and checked (issue 67, part 2): the system's, about the master key,
/// with the top of the copy as the id, for an administrative act and with no reference.
/// </summary>
public sealed class RecoveryTestedEventTests
{
    [Fact]
    public void TheEventIsTheSystemsAboutTheMasterKeyWithTheTopOfTheCopyAsTheIdAndNoReference()
    {
        var tested = CustodyEvent.RecoveryTested(42);

        Assert.Equal(
            (CustodyStep.RecoveryTested, CustodyItemKind.MasterKey, 42L, CustodyReason.AdministrativeAct, CustodyAuthor.System, null),
            (tested.Step, tested.Item.Kind, tested.Item.Id, tested.Reason, tested.Author, tested.Reference));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATopThatIsNotPositiveIsADefectOfTheCaller(long top) =>
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => CustodyEvent.RecoveryTested(top));

    [Fact]
    public void TheStepHasItsNameInTheChainAndIsOneOfTheStepsThatHaveAFactoryOfTheirOwn()
    {
        Assert.Equal("recovery_tested", CustodyNames.Of(CustodyStep.RecoveryTested));
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.MasterKey, 1), CustodyStep.RecoveryTested, CustodyReason.AdministrativeAct));
    }
}
