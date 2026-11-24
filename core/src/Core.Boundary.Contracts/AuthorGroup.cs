using Core.Platform.Logging;

namespace Core.Boundary.Contracts;

/// <summary>The coreference group of the name of one author of the item; the name itself never comes back as an occurrence.</summary>
/// <param name="Author">The name of the author, as the request carried it.</param>
/// <param name="Group">The group of every mention the service linked to that author.</param>
public sealed record AuthorGroup([property: Material] string Author, int Group)
{
    /// <summary>The group only; never the name.</summary>
    public override string ToString() => $"AuthorGroup {{ Group = {Group} }}";
}
