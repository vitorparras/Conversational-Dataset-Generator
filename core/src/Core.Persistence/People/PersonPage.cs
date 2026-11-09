namespace Core.Persistence.People;

/// <summary>One page of the list of people, in the order of the name: the page size is fixed, so the screen can declare it.</summary>
/// <param name="Items">The people of the page; empty when the registry, or the filter, leaves nobody.</param>
/// <param name="Total">How many people the filter leaves, over every page.</param>
/// <param name="Page">The page these items are, counted from one; an address beyond the last gives the last.</param>
public sealed record PersonPage(IReadOnlyList<ListedPerson> Items, int Total, int Page)
{
    /// <summary>How many people a page holds at most.</summary>
    public static int Size { get; } = 20;

    /// <summary>How many pages the filter leaves; one even when nobody is left, so the page 1 always exists.</summary>
    public int Pages => PagesFor(Total);

    /// <summary>How many pages a total of people takes, one at least.</summary>
    /// <param name="total">How many people the filter leaves.</param>
    public static int PagesFor(int total) => Math.Max(1, (total + Size - 1) / Size);
}
