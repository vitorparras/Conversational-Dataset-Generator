namespace Core.Persistence.People;

/// <summary>
/// What narrows the list of people, each part optional and all of them together: a stretch of the name (any case), the function declared exactly as the policy names it,
/// and whether the person is active.
/// </summary>
/// <param name="Name">A stretch of the name of use, found anywhere in it with no regard to case; null or blank for any name.</param>
/// <param name="Function">The function declared, letter by letter; null for any.</param>
/// <param name="Active">True for the active ones, false for the deactivated, null for both.</param>
public sealed record PersonFilter(string? Name, string? Function, bool? Active)
{
    /// <summary>No narrowing: everybody.</summary>
    public static PersonFilter Any { get; } = new(Name: null, Function: null, Active: null);
}
