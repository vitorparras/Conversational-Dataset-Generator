using Core.Ingestion.Deciding;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>An occurrence of the sanitized text, at the positions of that text, with its destination and the referent the mention refers to.</summary>
/// <param name="Decided">The occurrence carried to the sanitized text, with the destination decided for it.</param>
/// <param name="Referent">Who or what the mention refers to in the conversation.</param>
public sealed record ReferredOccurrence(DecidedOccurrence Decided, Referent Referent)
{
    /// <summary>The kind, the destination and where the referent comes from; never the offsets nor the name of an author.</summary>
    public override string ToString() => $"ReferredOccurrence {{ Decided = {Decided}, Referent = {Referent} }}";
}
