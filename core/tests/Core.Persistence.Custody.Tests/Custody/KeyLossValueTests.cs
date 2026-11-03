using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The scope and what was tried in the declaration of a loss (issue 67, CA-2): both come from fixed lists, and nothing outside them reaches the event.</summary>
public sealed class KeyLossValueTests
{
    [Theory]
    [InlineData("master")]
    [InlineData("texto-bruto/1/v1")]
    [InlineData("mapa-reversao/12/v3")]
    public void TheWholeKeyAndALabelAsTheCustodyFileWritesItAreScopes(string text)
    {
        Assert.True(KeyLossScope.TryRead(text, out var scope));
        Assert.Equal(text, scope!.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Master")]
    [InlineData("master ")]
    [InlineData("texto-bruto/0/v1")]
    [InlineData("texto-bruto/01/v1")]
    [InlineData("segredo/1/v1")]
    [InlineData("texto-bruto/1")]
    public void AnythingElseIsNotAScope(string? text)
    {
        Assert.False(KeyLossScope.TryRead(text, out var scope));
        Assert.Null(scope);
    }

    [Theory]
    [InlineData("copia-de-guarda", KeyLossAttempts.SafekeepingCopy)]
    [InlineData("copia-anterior-da-guarda", KeyLossAttempts.PreviousSafekeepingCopy)]
    [InlineData("registro-de-criacao-do-dominio", KeyLossAttempts.DomainCreationRecord)]
    [InlineData("copia-de-guarda,registro-de-criacao-do-dominio", KeyLossAttempts.SafekeepingCopy | KeyLossAttempts.DomainCreationRecord)]
    [InlineData("registro-de-criacao-do-dominio,copia-anterior-da-guarda,copia-de-guarda", KeyLossAttempts.All)]
    public void TheThingsTriedAreReadFromTheFixedList(string text, KeyLossAttempts expected)
    {
        Assert.True(KeyLossAttemptNames.TryRead(text, out var attempts));
        Assert.Equal(expected, attempts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(",")]
    [InlineData("copia-de-guarda,")]
    [InlineData("copia-de-guarda,copia-de-guarda")]
    [InlineData("copia-de-guarda,zqx-livre")]
    [InlineData("Copia-De-Guarda")]
    [InlineData("copia-de-guarda copia-anterior-da-guarda")]
    public void AnEmptyListARepeatedWordOrAWordOutsideTheListIsNotRead(string? text)
    {
        Assert.False(KeyLossAttemptNames.TryRead(text, out var attempts));
        Assert.Equal(KeyLossAttempts.None, attempts);
    }

    [Fact]
    public void TheWordsAcceptedAreThoseOfTheFixedListInOrder() =>
        Assert.Equal(["copia-de-guarda", "copia-anterior-da-guarda", "registro-de-criacao-do-dominio"], KeyLossAttemptNames.Accepted);

    [Fact]
    public void TheEventIsTheSystemsAboutTheMasterKeyWithWhatWasTriedAsTheIdAndTheScopeAsTheReference()
    {
        Assert.True(KeyLossScope.TryRead("mapa-reversao/2/v1", out var scope));

        var declared = CustodyEvent.KeyLossDeclared(scope!, KeyLossAttempts.SafekeepingCopy | KeyLossAttempts.DomainCreationRecord);

        Assert.Equal(
            (CustodyStep.KeyLossDeclared, CustodyItemKind.MasterKey, 5L, "mapa-reversao/2/v1", CustodyReason.AdministrativeAct, CustodyAuthor.System),
            (declared.Step, declared.Item.Kind, declared.Item.Id, declared.Reference, declared.Reason, declared.Author));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void ANoneOrUnknownSetOfAttemptsIsADefectOfTheCaller(int attempts) =>
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => CustodyEvent.KeyLossDeclared(KeyLossScope.Master, (KeyLossAttempts)attempts));

    [Fact]
    public void ANullScopeIsADefectOfTheCaller() =>
        Assert.Throws<ArgumentNullException>(() => CustodyEvent.KeyLossDeclared(null!, KeyLossAttempts.SafekeepingCopy));
}
