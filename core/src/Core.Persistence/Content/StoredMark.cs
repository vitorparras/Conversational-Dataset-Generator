namespace Core.Persistence.Content;

/// <summary>The mark of a message whose content is replicated, with the first occurrence of that content; the first occurrence is marked too, and points to itself.</summary>
/// <param name="Item">The item that holds the marked message.</param>
/// <param name="Position">The position of the marked message in its item.</param>
/// <param name="FirstItem">The item that holds the first occurrence.</param>
/// <param name="FirstPosition">The position of the first occurrence in its item.</param>
public sealed record StoredMark(long Item, int Position, long FirstItem, int FirstPosition);
