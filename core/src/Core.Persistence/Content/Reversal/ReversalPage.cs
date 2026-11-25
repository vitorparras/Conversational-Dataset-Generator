namespace Core.Persistence.Content.Reversal;

/// <summary>One page of requests, the newest first.</summary>
/// <param name="Items">The requests of the page.</param>
/// <param name="Total">How many the list leaves, over every page.</param>
public sealed record ReversalPage(IReadOnlyList<ListedReversal> Items, int Total)
{
    /// <summary>How many requests a page holds at most.</summary>
    public static int Size { get; } = 20;
}
