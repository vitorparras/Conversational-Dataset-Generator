using Core.Persistence.Content;

namespace Core.Persistence.Tests.Content;

/// <summary>The list of queries is read from the constants of the class, so a query nobody remembered to register is still in it.</summary>
public sealed class ContentQueriesTests
{
    [Fact]
    public void EveryStringConstantOfTheTypeIsInTheListWhateverItsVisibility()
    {
        var declared = ContentQueries.Declared(typeof(WithQueries));

        Assert.Equal(
            new Dictionary<string, string> { ["Open"] = "SELECT 1", ["Hidden"] = "SELECT 2", ["Internal"] = "SELECT 3" },
            declared.ToDictionary());
    }

    [Fact]
    public void FieldsThatAreNotStringConstantsAreNotQueries()
    {
        var names = ContentQueries.Declared(typeof(WithQueries)).Keys;

        Assert.DoesNotContain(nameof(WithQueries.NotConstant), names);
        Assert.DoesNotContain(nameof(WithQueries.Number), names);
    }

    [Fact]
    public void ListOfTheSystemHoldsEveryConstantOfTheClass()
    {
        Assert.Equal(ContentQueries.Declared(typeof(ContentQueries)).ToDictionary(), ContentQueries.All.ToDictionary());
    }

    private static class WithQueries
    {
        public const string Open = "SELECT 1";

        public const int Number = 7;

        internal const string Internal = "SELECT 3";

        public static readonly string NotConstant = "SELECT 4";

        private const string Hidden = "SELECT 2";
    }
}
