using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>The vocabulary of the kinds is the one the language service emits, written exactly so: a name that drifts turns every occurrence of it into one that is removed, and nothing else would say it.</summary>
public sealed class OccurrenceKindsTests
{
    [Theory]
    [InlineData("PERSON")]
    [InlineData("LOCATION")]
    [InlineData("ORGANIZATION")]
    [InlineData("CREDENTIAL")]
    [InlineData("SECRET")]
    [InlineData("PASSWORD")]
    [InlineData("TOKEN")]
    [InlineData("API_KEY")]
    [InlineData("PRIVATE_KEY")]
    public void EveryKindOfTheListIsKnown(string kind) => Assert.True(OccurrenceKinds.IsKnown(kind));

    [Theory]
    [InlineData("CREDENTIAL")]
    [InlineData("SECRET")]
    [InlineData("PASSWORD")]
    [InlineData("TOKEN")]
    [InlineData("API_KEY")]
    [InlineData("PRIVATE_KEY")]
    public void EverySecretOfTheListIsASecret(string kind) => Assert.True(OccurrenceKinds.IsSecret(kind));

    [Theory]
    [InlineData("PERSON")]
    [InlineData("LOCATION")]
    [InlineData("ORGANIZATION")]
    [InlineData("person")]
    [InlineData("CPF")]
    [InlineData("")]
    public void NoNameAndNothingOutsideTheListIsASecret(string kind) => Assert.False(OccurrenceKinds.IsSecret(kind));

    [Theory]
    [InlineData("person")]
    [InlineData("Location")]
    [InlineData("PER")]
    [InlineData("CPF")]
    [InlineData(" PERSON")]
    [InlineData("")]
    public void ACaseOrANameOutsideTheListIsUnknown(string kind) => Assert.False(OccurrenceKinds.IsKnown(kind));

    [Fact]
    public void TheNamesTheCoreUsesAreTheOnesTheListHas() =>
        Assert.Equal(["PERSON", "LOCATION", "ORGANIZATION"], [OccurrenceKinds.Person, OccurrenceKinds.Location, OccurrenceKinds.Organization]);
}
