using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event that records a verification, and the refusal the chain gives while the last verification failed: both are named, written by the system, and carry no content.</summary>
public sealed class CustodyVerificationEventTests
{
    [Fact]
    public void PassedVerificationIsTheSystemOnTheChainWithTheNumberOfEventsAndNoEventPointedAt()
    {
        var recorded = CustodyEvent.Verification(12, null);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 12), CustodyStep.VerifyChain, CustodyReason.VerificationPassed, null),
            (recorded.Author, recorded.Item, recorded.Step, recorded.Reason, recorded.Corrects));
    }

    [Fact]
    public void FailedVerificationPointsAtTheFirstInconsistentEvent()
    {
        var recorded = CustodyEvent.Verification(12, 5);

        Assert.Equal((CustodyReason.VerificationFailed, 5), (recorded.Reason, recorded.Corrects));
    }

    [Fact]
    public void VerificationOfAnEmptyChainIsAllowedAndNegativeOrZeroReferencesAreNot()
    {
        Assert.Equal(0, CustodyEvent.Verification(0, null).Item.Id);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(-1, null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(3, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(3, -2));
    }

    [Fact]
    public void ThatStepIsNotWrittenByTheOrdinaryConstructor() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.Person(1), new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.VerifyChain, CustodyReason.AdministrativeAct));

    [Fact]
    public void RefusalWhileTheLastVerificationFailedIsTheNamedResultAndNotTheServersText()
    {
        var failed = new PostgresException("the custody chain failed its last verification: no event is added until a verification passes", "ERROR", "ERROR", "CE002");
        var pending = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);

        var result = CustodyTransaction.Refused(failed, pending);

        Assert.Equal(CustodyOutcome.ChainBroken, result.Outcome);
        Assert.Null(result.Sequence);
        Assert.Equal(CustodyRefusals.ChainBroken().Refusal, result.Refusal);
        Assert.EndsWith(".", result.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlStateOfTheRefusalIsNotTheOneOfTheImmutability() =>
        Assert.NotEqual(CustodyStatements.ImmutableSqlState, CustodyStatements.ChainBrokenSqlState);

    [Fact]
    public void ContentRefusedWhileTheLastVerificationFailedSaysSoWithNothingOfTheContent()
    {
        var refusal = new CustodyChainBrokenException();

        Assert.Contains("nenhum conteúdo é gravado", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesOfTheVerificationAreTheOnesTheDatabaseListsAndNothingFree()
    {
        Assert.Equal(
            ("custody_chain", "verify_chain", "verification_passed", "verification_failed"),
            (CustodyNames.Of(CustodyItemKind.CustodyChain), CustodyNames.Of(CustodyStep.VerifyChain),
                CustodyNames.Of(CustodyReason.VerificationPassed), CustodyNames.Of(CustodyReason.VerificationFailed)));
    }
}
