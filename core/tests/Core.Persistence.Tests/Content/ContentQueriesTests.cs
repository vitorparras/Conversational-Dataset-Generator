using Core.Persistence.Content;

namespace Core.Persistence.Tests.Content;

/// <summary>The list of queries is read from the constants of the class, so a query nobody remembered to register is still in it.</summary>
public sealed class ContentQueriesTests
{
    [Fact]
    public void EveryPublicStringConstantOfTheTypeIsInTheList()
    {
        var declared = ContentQueries.Declared(typeof(DeclaresQueries));

        Assert.Equal(
            new Dictionary<string, string> { ["Open"] = "SELECT 1", ["Second"] = "SELECT 2" },
            declared.ToDictionary());
    }

    [Fact]
    public void FieldsThatAreNotPublicStringConstantsAreNotQueries()
    {
        var names = ContentQueries.Declared(typeof(DeclaresQueries)).Keys;

        Assert.DoesNotContain(nameof(DeclaresQueries.NotConstant), names);
        Assert.DoesNotContain(nameof(DeclaresQueries.Number), names);
        Assert.DoesNotContain("Internal", names);
    }

    [Fact]
    public void ListOfTheSystemHoldsEveryConstantOfTheClass() =>
        Assert.Equal(ContentQueries.Declared(typeof(ContentQueries)).ToDictionary(), ContentQueries.All.ToDictionary());
}

/// <summary>A class shaped like the list of queries, with what must and what must not be read from it.</summary>
internal static class DeclaresQueries
{
    public const string Open = "SELECT 1";

    public const string Second = "SELECT 2";

    public const int Number = 7;

    internal const string Internal = "SELECT 3";

    public static readonly string NotConstant = string.Concat("SELECT", " 4");
}
