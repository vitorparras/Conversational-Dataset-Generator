using System.Globalization;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// Who or what a mention refers to within one conversation: an author, by the name the material gives, or a group of mentions the coreference joined. The identity of a
/// referent is not the text of a mention, so two referents of the same name are two referents and get two markers.
/// </summary>
public sealed record Referent
{
    private Referent(ReferentSource source, string identity)
    {
        Source = source;
        Identity = identity;
    }

    /// <summary>Where the identity of the referent comes from.</summary>
    public ReferentSource Source { get; }

    private string Identity { get; }

    /// <summary>The author of messages of the conversation, by the name the material gives.</summary>
    /// <param name="name">The name of the author in the material.</param>
    public static Referent Author(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new Referent(ReferentSource.Author, name);
    }

    /// <summary>A group of mentions the coreference joined in the conversation.</summary>
    /// <param name="number">The number of the group in the conversation.</param>
    public static Referent Group(int number) => new(ReferentSource.Group, number.ToString(CultureInfo.InvariantCulture));

    /// <summary>The same referent: the same source and the same identity within it.</summary>
    public bool Equals(Referent? other) => other is not null && Source == other.Source && string.Equals(Identity, other.Identity, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Source, StringComparer.Ordinal.GetHashCode(Identity));

    /// <summary>Only where the referent comes from: the name of an author is never told.</summary>
    public override string ToString() => $"Referent {{ Source = {Source} }}";
}
