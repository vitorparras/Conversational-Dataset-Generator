using Core.Ingestion.Deciding;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>
/// The destination of one occurrence: three and no other, a secret always removed, and the same word in the function of a person and of a place
/// with different destinations (issue 84, CA-2 and CA-3).
/// </summary>
public sealed class DestinationPolicyTests
{
    [Theory]
    [InlineData("CREDENTIAL")]
    [InlineData("SECRET")]
    [InlineData("PASSWORD")]
    [InlineData("TOKEN")]
    [InlineData("API_KEY")]
    [InlineData("PRIVATE_KEY")]
    [InlineData("credential")]
    [InlineData("  Secret ")]
    public void ASecretIsRemovedEvenWhenItLooksLikeAProperNameInTheRoleOfAPerson(string kind) =>
        Assert.Equal(Destination.Remove, DestinationPolicy.Of(Found.Occurrence(kind, "PROPN", "nsubj")));

    [Fact]
    public void ThePersonIsPseudonymizedAndThePlaceIsPreservedForTheSameWord()
    {
        var person = DestinationPolicy.Of(Found.Occurrence("PERSON", "PROPN", "obl"));
        var place = DestinationPolicy.Of(Found.Occurrence("LOCATION", "PROPN", "obl"));

        Assert.Equal((Destination.Pseudonymize, Destination.Preserve), (person, place));
    }

    [Theory]
    [InlineData("nsubj")]
    [InlineData("nsubj:pass")]
    [InlineData("OBJ")]
    [InlineData("iobj")]
    [InlineData("vocative")]
    [InlineData("csubj")]
    public void APlaceInTheRoleOfWhoActsOrIsAddressedIsAPersonInThatSentence(string role) =>
        Assert.Equal(Destination.Pseudonymize, DestinationPolicy.Of(Found.Occurrence("LOCATION", "PROPN", role)));

    [Theory]
    [InlineData("obl")]
    [InlineData("obl:arg")]
    [InlineData("nmod")]
    [InlineData("advmod")]
    [InlineData("")]
    public void APlaceOutsideTheRolesOfWhoTakesPartIsPreserved(string role) =>
        Assert.Equal(Destination.Preserve, DestinationPolicy.Of(Found.Occurrence("location", "propn", role)));

    [Theory]
    [InlineData("ORGANIZATION")]
    [InlineData("PERSON")]
    public void APersonAndAnOrganizationKeepTheirRelationThroughAMarker(string kind) =>
        Assert.Equal(Destination.Pseudonymize, DestinationPolicy.Of(Found.Occurrence(kind, "PROPN", "obl")));

    [Theory]
    [InlineData("PERSON", "NOUN")]
    [InlineData("PERSON", "PRON")]
    [InlineData("LOCATION", "NOUN")]
    [InlineData("LOCATION", "")]
    public void WhatIsNotAProperNameIsRemovedBecauseThereIsNoNameToKeepARelationTo(string kind, string partOfSpeech) =>
        Assert.Equal(Destination.Remove, DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, "obl")));

    [Theory]
    [InlineData("CPF")]
    [InlineData("EMAIL")]
    [InlineData("UNHEARD_OF")]
    [InlineData("")]
    public void AKindThePolicyDoesNotKnowIsRemovedSoThatItCannotPassAsClean(string kind) =>
        Assert.Equal(Destination.Remove, DestinationPolicy.Of(Found.Occurrence(kind, "PROPN", "obl")));

    [Fact]
    public void ThereAreThreeDestinationsAndNoOther() =>
        Assert.Equal([Destination.Remove, Destination.Pseudonymize, Destination.Preserve], Enum.GetValues<Destination>());

    [Fact]
    public void NoOccurrenceIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => DestinationPolicy.Of(null!));

        Assert.Equal("occurrence", defect.ParamName);
    }
}
