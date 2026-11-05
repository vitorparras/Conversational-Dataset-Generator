using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The events of an entry (issue 71): the attempt accepted is the person's, the one refused is the person's only for a wrong password and the system's for a name nobody goes by.</summary>
public sealed class CustodySignInEventTests
{
    private const string RefusalMessage = "An entry is refused for a wrong password, of a person, or for a name nobody goes by, of nobody.";

    private const string OfTheirOwnMessage =
        "A correction, a verification, an emptying, a rotation, the creation of a domain, a seal, an entry, the loss of a key, a rehearsal of the recovery, "
        + "a conference of the guarantees, a window of the owner, a restore, a rehearsal of it, a revocation of a label and a rewrap of a domain have an event of their own; use Correction, Verification, "
        + "Emptied, EmptyingRefused, EpochRotated, DomainCreated, DomainLabelRotated, DomainLabelRevoked, DomainRewrapped, SealRecorded, SignInAccepted, SignInRefused, KeyLossDeclared, RecoveryRehearsed, "
        + "GuaranteesVerified, OwnerWindowOpened, OwnerWindowClosed, Restored or RestoreRehearsed.";

    [Fact]
    public void AcceptedEntryIsThePersonsAboutThatAttemptForAcceptedCredentials()
    {
        var custodyEvent = CustodyEvent.SignInAccepted(7, 41);

        Assert.Equal(
            (CustodyAuthor.Person(7), new CustodyItem(CustodyItemKind.SignInAttempt, 41), CustodyStep.SignInAccepted, CustodyReason.CredentialsAccepted, null, null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference, custodyEvent.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AcceptedEntryOfAnAttemptWithoutAPositiveNumberIsRefusedByTheAttemptParameter(long attemptId)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.SignInAccepted(7, attemptId));

        Assert.Equal("attemptId", refused.ParamName);
    }

    [Fact]
    public void RefusedEntryForAWrongPasswordIsThePersonsAboutThatAttempt()
    {
        var custodyEvent = CustodyEvent.SignInRefused(7, CustodyReason.PasswordWrong, 41);

        Assert.Equal(
            (CustodyAuthor.Person(7), new CustodyItem(CustodyItemKind.SignInAttempt, 41), CustodyStep.SignInRefused, CustodyReason.PasswordWrong, null, null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference, custodyEvent.Corrects));
    }

    [Fact]
    public void RefusedEntryForANameNobodyGoesByIsTheSystemsAboutThatAttempt()
    {
        var custodyEvent = CustodyEvent.SignInRefused(null, CustodyReason.AccountNotRecognized, 41);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.SignInAttempt, 41), CustodyStep.SignInRefused, CustodyReason.AccountNotRecognized, null, null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference, custodyEvent.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefusedEntryOfAnAttemptWithoutAPositiveNumberIsRefusedByTheAttemptParameter(long attemptId)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.SignInRefused(7, CustodyReason.PasswordWrong, attemptId));

        Assert.Equal("attemptId", refused.ParamName);
    }

    [Theory]
    [InlineData(CustodyReason.PasswordWrong, null)]
    [InlineData(CustodyReason.AccountNotRecognized, 7L)]
    [InlineData(CustodyReason.CredentialsAccepted, null)]
    [InlineData(CustodyReason.CredentialsAccepted, 7L)]
    [InlineData(CustodyReason.AdministrativeAct, null)]
    [InlineData(CustodyReason.AdministrativeAct, 7L)]
    [InlineData(CustodyReason.ContentRead, null)]
    [InlineData(CustodyReason.ContentRead, 7L)]
    public void RefusedEntryForAnyOtherPairOfReasonAndPersonIsRefusedWithTheTableAndTheReasonParameter(CustodyReason reason, long? personId)
    {
        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.SignInRefused(personId, reason, 41));

        Assert.Equal("reason", refused.ParamName);
        Assert.StartsWith(RefusalMessage, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CustodyStep.Correction)]
    [InlineData(CustodyStep.VerifyChain)]
    [InlineData(CustodyStep.ItemEmptied)]
    [InlineData(CustodyStep.ItemEmptyingRefused)]
    [InlineData(CustodyStep.EpochRotated)]
    [InlineData(CustodyStep.DomainCreated)]
    [InlineData(CustodyStep.SealRecorded)]
    [InlineData(CustodyStep.DomainLabelRotated)]
    [InlineData(CustodyStep.SignInAccepted)]
    [InlineData(CustodyStep.SignInRefused)]
    [InlineData(CustodyStep.KeyLossDeclared)]
    [InlineData(CustodyStep.RecoveryTested)]
    [InlineData(CustodyStep.VerifyGuarantees)]
    [InlineData(CustodyStep.OwnerWindowOpened)]
    [InlineData(CustodyStep.OwnerWindowClosed)]
    [InlineData(CustodyStep.Restored)]
    [InlineData(CustodyStep.RestoreRehearsed)]
    public void StepsThatHaveAnEventOfTheirOwnAreRefusedAsAPlainEventWithTheMessageNamingEveryFactoryAndTheStepParameter(CustodyStep step)
    {
        var item = new CustodyItem(CustodyItemKind.SignInAttempt, 41);

        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.Person(7), item, step, CustodyReason.CredentialsAccepted));

        Assert.Equal(("step", $"{OfTheirOwnMessage} (Parameter 'step')"), (refused.ParamName, refused.Message));
    }
}
