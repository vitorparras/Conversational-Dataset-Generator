using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// The referent of each mention, from the group the service gave it (decision 0060): a mention in the group of an author refers to that author, and so takes the marker of the author;
/// every other group refers to itself. The name rule leaves a nickname in a group of its own, so the nickname of an author never takes the marker of the author.
/// </summary>
public static class MentionReferents
{
    /// <summary>The mention with the referent it refers to.</summary>
    /// <param name="decided">A mention of an item analysed whole, with its group.</param>
    /// <param name="authors">The group of each author of the item, as the service gave it.</param>
    /// <exception cref="ArgumentException">The mention has no group: its text was analysed alone.</exception>
    public static ReferredOccurrence Refer(DecidedOccurrence decided, IReadOnlyList<AuthorGroup> authors)
    {
        ArgumentNullException.ThrowIfNull(decided);
        ArgumentNullException.ThrowIfNull(authors);
        if (decided.Group is not { } group)
        {
            throw new ArgumentException("Only a mention with its group has a referent.", nameof(decided));
        }

        var author = authors.FirstOrDefault(candidate => candidate.Group == group);
        return new ReferredOccurrence(decided, author is null ? Referent.Group(group) : Referent.Author(author.Author));
    }
}
