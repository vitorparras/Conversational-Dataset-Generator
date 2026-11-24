using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>
/// The destination of one occurrence: three and no other, a secret always removed, a proper name of a person, an organization or a place pseudonymized (decision 0056),
/// and the grammar breaking the tie between a name and a word that is none (issue 84, CA-2 and CA-3; issue 100, CA-5).
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
    public void ASecretIsRemovedEvenWhenItLooksLikeAProperNameInTheRoleOfAPerson(string kind) =>
        Assert.Equal(Destination.Remove, DestinationPolicy.Of(Found.Occurrence(kind, "PROPN", "nsubj")));

    [Fact]
    public void ProperNameOfAPlaceIsPseudonymized()
    {
        var person = DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Person, "PROPN", "obl"));
        var place = DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Location, "PROPN", "obl"));

        Assert.Equal((Destination.Pseudonymize, Destination.Pseudonymize), (person, place));
    }

    [Theory]
    [InlineData("nsubj")]
    [InlineData("nsubj:pass")]
    [InlineData("obj")]
    [InlineData("iobj")]
    [InlineData("vocative")]
    [InlineData("obl")]
    [InlineData("nmod")]
    [InlineData("")]
    public void APlaceIsPseudonymizedInWhateverFunctionItHasInTheSentence(string role) =>
        Assert.Equal(Destination.Pseudonymize, DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Location, "PROPN", role)));

    [Theory]
    [InlineData("nsubj")]
    [InlineData("obj")]
    [InlineData("obl")]
    [InlineData("")]
    public void APersonIsPseudonymizedInWhateverFunctionItHasInTheSentence(string role) =>
        Assert.Equal(Destination.Pseudonymize, DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Person, "PROPN", role)));

    [Fact]
    public void AnOrganizationKeepsItsRelationThroughAMarker() =>
        Assert.Equal(Destination.Pseudonymize, DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Organization, "PROPN", "obl")));

    [Theory]
    [InlineData("PERSON", "NOUN")]
    [InlineData("PERSON", "PRON")]
    [InlineData("ORGANIZATION", "NOUN")]
    [InlineData("LOCATION", "NOUN")]
    [InlineData("LOCATION", "")]
    public void WhatIsNotAProperNameIsRemovedBecauseThereIsNoNameToKeepARelationTo(string kind, string partOfSpeech) =>
        Assert.Equal(Destination.Remove, DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, "obl")));

    [Theory]
    [InlineData("CPF")]
    [InlineData("PER")]
    [InlineData("person")]
    [InlineData("")]
    public void AKindOutsideTheListIsRemovedSoThatItCannotPassAsClean(string kind) =>
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
