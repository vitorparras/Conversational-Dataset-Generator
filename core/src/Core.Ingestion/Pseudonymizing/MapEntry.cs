namespace Core.Ingestion.Pseudonymizing;

/// <summary>One entry of the map of an item: the referent, the marker written in its place and the original the marker replaced.</summary>
/// <param name="Referent">Who or what the mention refers to in the conversation.</param>
/// <param name="Marker">The marker written in place of the original.</param>
/// <param name="Original">The span of the sanitized text the marker replaced.</param>
public sealed record MapEntry(Referent Referent, Marker Marker, string Original)
{
    /// <summary>Whether the referent is an author of the conversation.</summary>
    public bool IsAuthor => Referent.Source == ReferentSource.Author;

    /// <summary>The marker only: the original is what the map exists to keep out of sight.</summary>
    public override string ToString() => $"MapEntry {{ Marker = {Marker} }}";
}
