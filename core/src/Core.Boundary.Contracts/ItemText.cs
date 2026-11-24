using Core.Platform.Logging;

namespace Core.Boundary.Contracts;

/// <summary>One text of an item, with the name of who wrote it when the item is a conversation.</summary>
/// <param name="Text">The text to analyse, before sanitization.</param>
/// <param name="Author">The name of the author as the material carries it; null when the text has no author.</param>
public sealed record ItemText([property: Material] string Text, [property: Material] string? Author)
{
    /// <summary>A size in UTF-16 units for diagnosis only and whether there is an author; never the text nor the name.</summary>
    public override string ToString() => $"ItemText {{ TextChars = {Text.Length}, HasAuthor = {Author is not null} }}";
}
