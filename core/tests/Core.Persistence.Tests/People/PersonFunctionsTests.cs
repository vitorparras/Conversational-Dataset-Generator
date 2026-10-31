using Core.Persistence.People;

namespace Core.Persistence.Tests.People;

/// <summary>The functions are the six of the access policy, taken letter by letter: a name that only looks alike is not one of them.</summary>
public sealed class PersonFunctionsTests
{
    [Fact]
    public void SixDistinctFunctionsAreAccepted()
    {
        Assert.Equal(6, PersonFunctions.Accepted.Count);
        Assert.Equal(6, PersonFunctions.Accepted.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryFunctionTheListNamesIsAccepted()
    {
        var refused = PersonFunctions.Accepted.Where(function => !PersonFunctions.IsAccepted(function));

        Assert.Empty(refused);
    }

    [Theory]
    [InlineData("")]
    [InlineData("anotação")]
    [InlineData("Anotacao")]
    [InlineData(" Anotação")]
    [InlineData("Anotação ")]
    [InlineData("Dono")]
    public void ANameThatIsNotExactlyOneOfThemIsRefused(string function) =>
        Assert.False(PersonFunctions.IsAccepted(function));
}
