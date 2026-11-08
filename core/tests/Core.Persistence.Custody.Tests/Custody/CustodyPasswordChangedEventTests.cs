using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The event of a person who changed their own password (issue 597): theirs as the author and the item, for an administrative act,
/// with the sessions it ended as the reference and no value of a password.
/// </summary>
public sealed class CustodyPasswordChangedEventTests
{
    [Theory]
    [InlineData(7, 0, "0")]
    [InlineData(7, 3, "3")]
    [InlineData(12, 120, "120")]
    public void TheChangeIsTheirsAboutThemselvesWithTheSessionsEndedAsTheReference(long personId, int ended, string reference)
    {
        var changed = CustodyEvent.PasswordChanged(personId, ended);

        Assert.Equal(
            (CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.Person, personId), CustodyStep.PasswordChanged, CustodyReason.AdministrativeAct, reference, null),
            (changed.Author, changed.Item, changed.Step, changed.Reason, changed.Reference, changed.Corrects));
    }

    [Theory]
    [InlineData(0, 1, "personId")]
    [InlineData(-3, 1, "personId")]
    [InlineData(7, -1, "sessionsEnded")]
    public void ANumberThatIsNoPersonOrNoCountIsRefusedByItsParameter(long personId, int ended, string parameter)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.PasswordChanged(personId, ended));

        Assert.Equal(parameter, refused.ParamName);
    }

    [Fact]
    public void TheChangeAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.Person(7), new CustodyItem(CustodyItemKind.Person, 7), CustodyStep.PasswordChanged, CustodyReason.AdministrativeAct));

    [Fact]
    public void TheStepIsWrittenUnderItsName() =>
        Assert.Equal("password_changed", CustodyNames.Of(CustodyStep.PasswordChanged));
}
