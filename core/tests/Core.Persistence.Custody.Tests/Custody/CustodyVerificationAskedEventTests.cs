using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The verification someone asked for (#605, CA-4): the same verdict as the one the host runs by itself, still written by the system as the chain requires of the step, with the id of whoever asked
/// in the reference, which is the one place the history reads it from.
/// </summary>
public sealed class CustodyVerificationAskedEventTests
{
    [Fact]
    public void AskedVerificationIsTheSameVerdictWrittenByTheSystemWithTheAskerInTheReference()
    {
        var recorded = CustodyEvent.Verification(12, null, 7);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 12), CustodyStep.VerifyChain, CustodyReason.VerificationPassed, null, "requested-by/7"),
            (recorded.Author, recorded.Item, recorded.Step, recorded.Reason, recorded.Corrects, recorded.Reference));
    }

    [Fact]
    public void AskedVerificationThatFailedPointsAtTheFirstInconsistentEventAndKeepsTheAsker()
    {
        var recorded = CustodyEvent.Verification(12, 5, 7);

        Assert.Equal((CustodyReason.VerificationFailed, 5, "requested-by/7"), (recorded.Reason, recorded.Corrects, recorded.Reference));
    }

    [Fact]
    public void VerificationTheHostRunsByItselfHasNoReference() => Assert.Null(CustodyEvent.Verification(12, null).Reference);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AskerThatIsNotAPositiveIdIsRefused(long asker) => _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(12, null, asker));

    [Fact]
    public void TheReferenceNamesTheAskerOnlyWhenItIsOursAndPositive()
    {
        long?[] expected = [7L, 9_223_372_036_854_775_807L, null, null, null, null, null, null];
        long?[] actual =
            [
                RequesterReference.From("requested-by/7"),
                RequesterReference.From("requested-by/9223372036854775807"),
                RequesterReference.From("requested-by/0"),
                RequesterReference.From("requested-by/-3"),
                RequesterReference.From("requested-by/7x"),
                RequesterReference.From("requested-by/"),
                RequesterReference.From("seal/7"),
                RequesterReference.From(null),
            ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NobodyAskingGivesNoReference() => Assert.Null(RequesterReference.Of(null));
}
